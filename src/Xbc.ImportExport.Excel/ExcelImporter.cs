using ClosedXML.Excel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Xbc.ImportExport.Excel
{
    /// <summary>Excel 导入器（基于 ClosedXML，支持 .xlsx 格式逐行解析与导入模板生成）。</summary>
    public sealed class ExcelImporter : IImporter
    {
        /// <summary>允许值下拉的隐藏工作表名（字面列表超长时承载引用区域）。</summary>
        private const string AllowedValuesSheetName = "下拉选项";

        /// <summary>Excel 数据校验公式上限（255 字符）；字面列表超限时改写隐藏工作表的引用区域。</summary>
        private const int ValidationFormulaMaxLength = 255;

        /// <inheritdoc />
        public IAsyncEnumerable<ParsedRow<T>> ImportAsync<T>(
            Stream stream,
            ImportOptions options,
            CancellationToken cancellationToken = default) where T : class, new()
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            // 参数即时校验（与 IExporter 对称）：迭代器体内的校验会延迟到首次枚举，拆分公共方法与私有迭代器
            return ImportCoreAsync<T>(stream, options, cancellationToken);
        }

#pragma warning disable CS1998 // async 修饰符为 IAsyncEnumerable 所需；ClosedXML 为同步 API，无真实 await
        private async IAsyncEnumerable<ParsedRow<T>> ImportCoreAsync<T>(
            Stream stream,
            ImportOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken) where T : class, new()
#pragma warning restore CS1998
        {
            var effectiveOptions = options ?? new ImportOptions();
            var startRow = effectiveOptions.StartRow > 0 ? effectiveOptions.StartRow : 1;
            var targetType = typeof(T);

            // 构造即读取流（不可读/已释放的流在此抛出），流不可用属调用方使用错误而非文件数据问题
            XLWorkbook workbook;
            try
            {
                workbook = new XLWorkbook(stream);
            }
            catch (Exception ex)
            {
                throw new ImportException($"Excel 工作簿打开失败：{ex.Message}", ex);
            }

            using (workbook)
            {
                // 无工作表属文件结构错误（连表头行的载体都不存在），快速失败（构造函数已解析完整包，取工作表集合不会再抛数据异常）
                if (!workbook.Worksheets.Any())
                    throw new ImportException("Excel 工作簿不包含任何工作表");

                // 工作表选择：指定名称按名匹配（大小写不敏感，不存在时快速失败并列出可用工作表），未指定取第一个
                IXLWorksheet worksheet;
                if (string.IsNullOrEmpty(effectiveOptions.SheetName))
                {
                    worksheet = workbook.Worksheets.FirstOrDefault();
                }
                else
                {
                    worksheet = workbook.Worksheets.FirstOrDefault(ws => string.Equals(ws.Name, effectiveOptions.SheetName, StringComparison.OrdinalIgnoreCase));
                    if (worksheet == null)
                    {
                        var availableNames = string.Join("、", workbook.Worksheets.Select(ws => ws.Name));
                        throw new ImportException($"工作表 '{effectiveOptions.SheetName}' 不存在（可用工作表：{availableNames}）");
                    }
                }

                // 初始化阶段：解析表头 + 构建映射（此处无 yield，可用 try-catch 包装 ClosedXML 异常）
                Dictionary<string, int> headerMap;
                List<ImportColumnMapping> propMappings;
                int dataStartRow;
                int lastRow;
                int lastCol;

                try
                {
                    // 1. 解析表头行，建立 列名 → 列索引 映射（无有效表头属文件结构错误，快速失败）
                    headerMap = BuildHeaderMap(worksheet, startRow);
                    if (headerMap.Count == 0)
                        throw new ImportException($"第 {startRow} 行无有效表头（起始行超出数据范围或表头列名全为空白）");

                    // 2. 构建列映射（列名 → 列索引 + PropertyInfo；程序化列优先）
                    propMappings = ImportColumnResolver.BuildMapping(
                        targetType,
                        effectiveOptions.Columns,
                        headerMap);

                    // 3. 准备数据行范围与空行扫描的列范围
                    dataStartRow = startRow + 1;
                    var lastRowUsed = worksheet.LastRowUsed();
                    lastRow = lastRowUsed?.RowNumber() ?? dataStartRow - 1;
                    lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
                }
                catch (Exception ex) when (ex is not ImportExportException)
                {
                    throw new ImportException($"Excel 表头解析失败：{ex.Message}", ex);
                }

                // 数据行计数（跳过的空行不计），小于等于 0 表示不限（默认 100000 防 OOM 由选项保证）
                var maxRows = effectiveOptions.MaxRows > 0 ? effectiveOptions.MaxRows : int.MaxValue;
                var dataRowCount = 0;

                // 4. 逐行解析数据
                // 注意：yield 不能出现在带 catch 的 try 块内，故解析在 try-catch 内完成，
                // continue 跳过空行后到此处 yield return（ClosedXML 异常须包装为 ImportException）。
                for (var row = dataStartRow; row <= lastRow; row++)
                {
                    ParsedRow<T> parsedRow;
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // 整行单元格均为空白视为空行（含未映射列，空白判定对齐 CSV 侧 IsNullOrWhiteSpace），按选项决定跳过还是产出默认值行
                        var isBlankRow = true;
                        for (var col = 1; col <= lastCol; col++)
                        {
                            if (!string.IsNullOrWhiteSpace(GetCellText(worksheet.Cell(row, col), null)))
                            {
                                isBlankRow = false;
                                break;
                            }
                        }

                        if (isBlankRow && effectiveOptions.SkipEmptyRows)
                            continue;

                        // 数据行超限按数据异常定位行号中断（跳过的空行不计入）
                        dataRowCount++;
                        if (dataRowCount > maxRows)
                            throw new ImportDataException($"导入行数超过上限 {maxRows}", row, "");

                        var entity = new T();
                        var rawValues = new Dictionary<string, string>();
                        var errors = new List<string>();

                        if (isBlankRow)
                        {
                            // 空行保留时产出默认值行（同样走解析路径，声明默认值生效）
                            foreach (var mapping in propMappings)
                            {
                                rawValues[mapping.Column.Name] = string.Empty;
                                var converted = ImportValueParser.Resolve(
                                    mapping, string.Empty, effectiveOptions.ErrorMode, row, errors);
                                if (converted != null)
                                    mapping.Column.Property.SetValue(entity, converted);
                            }
                        }
                        else
                        {
                            foreach (var mapping in propMappings)
                            {
                                var cell = worksheet.Cell(row, mapping.ColumnIndex);
                                var cellValue = GetCellText(cell, mapping.Column.Format);

                                rawValues[mapping.Column.Name] = cellValue;

                                // 按错误模式解析列值（宽容回退、严格抛出、收集记录）
                                var converted = ImportValueParser.Resolve(
                                    mapping, cellValue, effectiveOptions.ErrorMode, row, errors);
                                if (converted != null)
                                    mapping.Column.Property.SetValue(entity, converted);
                            }
                        }

                        parsedRow = new ParsedRow<T>
                        {
                            RowNumber = row,
                            Data = entity,
                            RawValues = rawValues,
                            Errors = errors
                        };
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException && ex is not ImportExportException)
                    {
                        throw new ImportDataException($"Excel 导入失败：{ex.Message}", row, "", ex);
                    }

                    yield return parsedRow;
                }
            }
        }

        private static Dictionary<string, int> BuildHeaderMap(IXLWorksheet worksheet, int headerRow)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var lastColUsed = worksheet.LastColumnUsed();
            var lastCol = lastColUsed?.ColumnNumber() ?? 0;

            // 索引是从 1 开始的
            for (var col = 1; col <= lastCol; col++)
            {
                var headerCell = worksheet.Cell(headerRow, col);
                var headerText = GetCellText(headerCell, null)?.Trim();
                if (string.IsNullOrEmpty(headerText))
                    continue;

                // 重复列名（大小写不敏感）无法建立唯一列映射，快速失败而非静默取最后一列
                if (map.ContainsKey(headerText))
                    throw new ImportException($"表头列名 '{headerText}' 在第 {col} 列重复出现，无法建立唯一的列映射");

                map[headerText] = col;
            }

            return map;
        }

        /// <summary>
        /// 按单元格值类型取文本（文化无关）：类型化单元格（数字/日期/布尔/时长）转 InvariantCulture 文本，
        /// 避免 GetString() 按 CurrentCulture 文本化（如 de-DE 下数字 1.5 → "1,5" 解析必败、日期被静默颠倒）；
        /// 日期与时长单元格在列声明 Format 时按该模式格式化（与解析侧 TryParseExact 对齐，导出→导入往返不因显示格式断裂）。
        /// </summary>
        private static string GetCellText(IXLCell cell, string format)
        {
            if (cell == null)
                return string.Empty;

            var value = cell.Value;
            switch (value.Type)
            {
                case XLDataType.Number:
                    return value.GetNumber().ToString(CultureInfo.InvariantCulture);

                case XLDataType.DateTime:
                    return string.IsNullOrEmpty(format)
                        ? value.GetDateTime().ToString(CultureInfo.InvariantCulture)
                        : value.GetDateTime().ToString(format, CultureInfo.InvariantCulture);

                case XLDataType.Boolean:
                    return value.GetBoolean().ToString(CultureInfo.InvariantCulture);

                case XLDataType.TimeSpan:
                    return string.IsNullOrEmpty(format)
                        ? value.GetTimeSpan().ToString(null, CultureInfo.InvariantCulture)
                        : value.GetTimeSpan().ToString(format, CultureInfo.InvariantCulture);

                case XLDataType.Text:
                    return value.GetText();

                default:
                    // Blank/Error 等：回落 GetString()（空单元格返回空串）
                    return cell.GetString();
            }
        }

        /// <inheritdoc />
        public Task<Stream> GenerateTemplateAsync<T>(
            TemplateOptions options = null,
            CancellationToken cancellationToken = default) where T : class, new()
        {
            var effectiveOptions = options ?? new TemplateOptions();
            var targetType = typeof(T);

            var columns = ImportColumnResolver.ResolveTemplateColumns(targetType);
            if (columns == null || columns.Count == 0)
                throw new TemplateException($"类型 '{targetType}' 未声明导入列（标记 [ImportColumn]）");

            cancellationToken.ThrowIfCancellationRequested();

            var sheetName = !string.IsNullOrEmpty(effectiveOptions.SheetName) ? effectiveOptions.SheetName : "导入模板";
            // Excel 工作表名上限 31 字符；超限前置校验给中文错误，与 ExcelExporter.AddSheet 对称
            // （不前置校验则依赖 ClosedXML 英文异常，且被下方通用 catch 包装丢失语义）
            if (sheetName.Length > 31)
                throw new TemplateException($"工作表名 '{sheetName}' 长度超过 Excel 的 31 字符上限");

            // 工作簿构造、保存与释放时序由 XlsxOutput 统一编排，写出内容以委托注入
            try
            {
                return Task.FromResult(XlsxOutput.Write(workbook =>
                {
                    var worksheet = workbook.Worksheets.Add(sheetName);

                    // 1. 写表头（加粗 + 背景色 + 必填标记）
                    for (var i = 0; i < columns.Count; i++)
                    {
                        var col = columns[i];
                        var cell = worksheet.Cell(1, i + 1);
                        cell.Value = col.Name;
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.LightBlue;

                        // 必填列用红色字体标记
                        if (col.IsRequired)
                            cell.Style.Font.FontColor = XLColor.Red;

                        // 列宽
                        worksheet.Column(i + 1).Width = col.Width > 0 ? col.Width : 15;
                    }

                    // 2. 数据校验下拉（枚举/允许值）
                    if (effectiveOptions.IncludeDataValidation)
                        AddDataValidations(workbook, worksheet, columns);

                    // 3. 示例行
                    if (effectiveOptions.IncludeExampleRows)
                        AddExampleRows(worksheet, columns);

                    // 4. 字段说明 Sheet
                    if (effectiveOptions.IncludeFieldDescription)
                        AddFieldDescriptionSheet(workbook, columns);
                }));
            }
            catch (Exception ex) when (ex is not OperationCanceledException && ex is not ImportExportException)
            {
                throw new TemplateException($"Excel 模板生成失败：{ex.Message}", ex);
            }
        }

        private static void AddDataValidations(IXLWorkbook workbook, IXLWorksheet worksheet, List<TemplateColumn> columns)
        {
            IXLWorksheet listSheet = null;
            var nextListColumn = 0;

            try
            {
                for (var i = 0; i < columns.Count; i++)
                {
                    var col = columns[i];
                    if (col.AllowedValues == null || col.AllowedValues.Count == 0)
                        continue;

                    var columnLetter = XLHelper.GetColumnLetterFromNumber(i + 1);
                    var range = worksheet.Range(columnLetter + "2:" + columnLetter + "1000");
                    var validation = range.CreateDataValidation();
                    validation.ErrorMessage = $"列 '{col.Name}' 值不在允许范围内";
                    validation.ErrorTitle = "输入无效";
                    validation.ErrorStyle = XLErrorStyle.Warning;

                    var literal = "\"" + string.Join(",", col.AllowedValues) + "\"";
                    if (literal.Length <= ValidationFormulaMaxLength)
                    {
                        // 字面列表须整体双引号包裹（OOXML 规范），裸写会被 Excel 判为非法公式引用并在打开时剥离
                        validation.List(literal);
                        continue;
                    }

                    // 字面列表超过 Excel 公式 255 字符上限时 Excel 打开会剥离校验，
                    // 改写隐藏工作表的独立列（每个字段一列，1 行起），下拉引用该区域绕开上限
                    if (listSheet == null)
                        listSheet = workbook.Worksheets.Add(AllowedValuesSheetName);

                    nextListColumn++;
                    var listColumnLetter = XLHelper.GetColumnLetterFromNumber(nextListColumn);
                    for (var v = 0; v < col.AllowedValues.Count; v++)
                        listSheet.Cell(v + 1, nextListColumn).Value = col.AllowedValues[v];

                    validation.List($"'{AllowedValuesSheetName}'!${listColumnLetter}$1:${listColumnLetter}${col.AllowedValues.Count}");
                }
            }
            finally
            {
                // 仅在确实写入了引用区域时创建并隐藏，短列表模板不产生额外工作表
                listSheet?.Hide();
            }
        }

        private static void AddExampleRows(IXLWorksheet worksheet, List<TemplateColumn> columns)
        {
            // 表头下写 1 行示例（允许值首个或按类型推断），灰色斜体标记
            for (var i = 0; i < columns.Count; i++)
            {
                var cell = worksheet.Cell(2, i + 1);
                cell.Value = columns[i].GetExampleValue();

                // 示例行用灰色斜体标记
                cell.Style.Font.Italic = true;
                cell.Style.Font.FontColor = XLColor.Gray;
            }

            // 示例行首个单元格挂批注，明示填写前删除示例行（避免示例数据混入真实导入）
            worksheet.Cell(2, 1).CreateComment().AddText("示例行，请删除后填写实际数据");
        }

        private static void AddFieldDescriptionSheet(IXLWorkbook workbook, List<TemplateColumn> columns)
        {
            var descSheet = workbook.Worksheets.Add("字段说明");
            descSheet.Cell(1, 1).Value = "列名";
            descSheet.Cell(1, 2).Value = "是否必填";
            descSheet.Cell(1, 3).Value = "数据类型";
            descSheet.Cell(1, 4).Value = "格式说明";
            descSheet.Cell(1, 5).Value = "允许值";
            descSheet.Cell(1, 6).Value = "说明";

            for (var i = 1; i <= 6; i++)
            {
                descSheet.Cell(1, i).Style.Font.Bold = true;
                descSheet.Cell(1, i).Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            for (var i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                var row = i + 2;
                descSheet.Cell(row, 1).Value = col.Name;
                descSheet.Cell(row, 2).Value = col.IsRequired ? "是" : "否";
                descSheet.Cell(row, 3).Value = col.DataType.ToString();
                descSheet.Cell(row, 4).Value = col.Format ?? string.Empty;
                descSheet.Cell(row, 5).Value = col.AllowedValues != null && col.AllowedValues.Count > 0
                    ? string.Join(", ", col.AllowedValues)
                    : string.Empty;
                descSheet.Cell(row, 6).Value = col.Comment ?? string.Empty;
            }

            descSheet.Columns().AdjustToContents();
        }

        /// <inheritdoc />
        public Task<Stream> GenerateErrorReportAsync<T>(
            IEnumerable<ParsedRow<T>> rows,
            CancellationToken cancellationToken = default) where T : class, new()
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));

            cancellationToken.ThrowIfCancellationRequested();

            var errorRows = ErrorReportSupport.CollectErrorRows(rows, out var headers);

            return Task.FromResult(XlsxOutput.Write(workbook =>
            {
                var worksheet = workbook.Worksheets.Add("错误回执");

                // 1. 写表头（样式对齐模板表头：加粗 + 背景色）
                for (var i = 0; i < headers.Count; i++)
                {
                    var cell = worksheet.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
                }

                // 2. 写错误行（整行浅黄底定位错误行，错误信息列红字）
                for (var r = 0; r < errorRows.Count; r++)
                {
                    var row = errorRows[r];
                    var excelRow = r + 2;

                    for (var i = 0; i < headers.Count; i++)
                    {
                        var cell = worksheet.Cell(excelRow, i + 1);
                        if (i == 0)
                        {
                            // 行号为数字单元格，回执内可直接排序
                            cell.Value = row.RowNumber;
                        }
                        else if (i == headers.Count - 1)
                        {
                            cell.Value = string.Join("；", row.Errors);
                        }
                        else
                        {
                            // 原始列按表头取值，缺键（如程序化列名与表头不一致）输出空串；公式触发文本经 EscapeFormula 防护
                            row.RawValues.TryGetValue(headers[i], out var rawValue);
                            cell.Value = EscapeFormula(rawValue ?? string.Empty);
                        }

                        cell.Style.Fill.BackgroundColor = XLColor.LightYellow;
                    }

                    worksheet.Cell(excelRow, headers.Count).Style.Font.FontColor = XLColor.Red;
                }

                // 3. 列宽自适应
                worksheet.Columns().AdjustToContents();
            }));
        }

        /// <summary>
        /// 公式触发文本加 ' 前缀（首字符为 =、+、-、@、制表符或回车；原始数据为用户输入，回执重新打开时不解释为公式）。
        /// ClosedXML 将 ' 前缀转为 quotePrefix 样式 + 纯文本存储（单元格值不含 '，Excel 显示层标记为文本）。
        /// </summary>
        private static string EscapeFormula(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            var c = value[0];
            return c == '=' || c == '+' || c == '-' || c == '@' || c == '\t' || c == '\r'
                ? "'" + value
                : value;
        }
    }
}
