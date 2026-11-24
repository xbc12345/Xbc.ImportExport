using ClosedXML.Excel;
using System.Globalization;

namespace Xbc.ImportExport.Excel
{
    /// <summary>Excel 导出器（基于 ClosedXML，逐项枚举数据源写入 .xlsx，避免全量实体驻留）。</summary>
    public sealed class ExcelExporter : IExporter
    {
        /// <inheritdoc />
        public async Task<Stream> ExportAsync<T>(
            IAsyncEnumerable<T> data,
            ExportOptions options,
            CancellationToken cancellationToken = default) where T : class
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var effectiveOptions = options ?? new ExportOptions();
            var targetType = typeof(T);

            // 解析生效列定义（程序化列优先并校验，否则回退特性解析）并逐列应用变换（ColumnTransformer 收到克隆隔离副本，null 保留原列）
            var columns = ExportColumnResolver.ResolveAndTransformColumns(effectiveOptions, targetType);
            if (columns == null || columns.Count == 0)
                throw new ExportException($"类型 '{targetType}' 未声明导出列（标记 [ExportColumn] 或在 ExportOptions.Columns 指定）");

            var sheetName = !string.IsNullOrEmpty(effectiveOptions.SheetName) ? effectiveOptions.SheetName : "Sheet1";
            // 单工作表数据行上限，0 表示不分片（每个工作表重复表头与列宽）
            var rowsPerSheet = effectiveOptions.MaxRowsPerSheet > 0 ? effectiveOptions.MaxRowsPerSheet : int.MaxValue;
            var headerOffset = effectiveOptions.IncludeHeader ? 1 : 0;

            // 工作簿构造、保存与释放时序由 XlsxOutput 统一编排，写出内容以委托注入
            try
            {
                return await XlsxOutput.WriteAsync(async workbook =>
                {
                    // 首个工作表（分片超出 MaxRowsPerSheet 时新建，命名为 基名-2/基名-3…）
                    var sheetIndex = 0;
                    var worksheet = AddSheet(workbook, sheetName, columns, effectiveOptions.IncludeHeader);

                    // 写数据行
                    var rowCount = 0;
                    var rowsInCurrentSheet = 0;
                    // 小于等于 0 表示不限（默认 100000 防 OOM 由选项保证）
                    var maxRows = effectiveOptions.MaxRows > 0 ? effectiveOptions.MaxRows : int.MaxValue;

                    await foreach (var item in data.WithCancellation(cancellationToken).ConfigureAwait(false))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (item == null)
                            continue;

                        if (rowCount >= maxRows)
                            throw new ExportException($"导出行数超过上限 {maxRows}");

                        // 当前工作表写满后自动分片（新工作表重复表头与列宽）
                        if (rowsInCurrentSheet >= rowsPerSheet)
                        {
                            sheetIndex++;
                            worksheet = AddSheet(workbook, sheetName + "-" + (sheetIndex + 1), columns, effectiveOptions.IncludeHeader);
                            rowsInCurrentSheet = 0;
                        }

                        WriteRow(worksheet, headerOffset + rowsInCurrentSheet + 1, item, columns);
                        rowsInCurrentSheet++;
                        rowCount++;
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException && ex is not ImportExportException)
            {
                throw new ExportException($"Excel 导出失败：{ex.Message}", ex);
            }
        }

        /// <summary>新建工作表并应用列宽与表头（分片时每个工作表重复应用）。</summary>
        private static IXLWorksheet AddSheet(XLWorkbook workbook, string sheetName, IList<ExportColumn> columns, bool includeHeader)
        {
            // Excel 工作表名上限 31 字符；基名过长或分片拼接序号（基名-2）后超长在此前置校验，超限给中文错误而非依赖底层库抛英文异常
            if (sheetName.Length > 31)
                throw new ExportException($"工作表名 '{sheetName}' 长度 {sheetName.Length} 超过 Excel 的 31 字符上限");

            var worksheet = workbook.Worksheets.Add(sheetName);

            // 设置列宽（列的属性，独立于表头，无表头导出同样生效）
            for (var i = 0; i < columns.Count; i++)
            {
                if (columns[i].Width.HasValue)
                    worksheet.Column(i + 1).Width = columns[i].Width.Value;
            }

            // 写表头
            if (includeHeader)
            {
                for (var i = 0; i < columns.Count; i++)
                {
                    var cell = worksheet.Cell(1, i + 1);
                    cell.Value = columns[i].Name;
                    cell.Style.Font.Bold = true;
                }
            }

            return worksheet;
        }

        private static void WriteRow<T>(IXLWorksheet worksheet, int rowIndex, T item, IList<ExportColumn> columns)
        {
            for (var i = 0; i < columns.Count; i++)
            {
                var column = columns[i];
                var cell = worksheet.Cell(rowIndex, i + 1);

                var value = column.Property.GetValue(item);
                if (value == null)
                    continue;

                // 值转换器优先
                if (column.Converter != null)
                    value = column.Converter(value);

                SetCellValue(cell, value, column.Format);
            }
        }

        private static void SetCellValue(IXLCell cell, object value, string format)
        {
            switch (value)
            {
                case string s:
                    cell.Value = s;
                    break;
                case bool b:
                    cell.Value = b;
                    break;
                case DateTime dt:
                    cell.Value = dt;
                    ApplyNumberFormat(cell, format);
                    break;
                case DateTimeOffset dto:
                    // Excel 无时区语义，取钟面时间（与导入侧解析按钟面往返一致，偏移不保留）
                    cell.Value = dto.DateTime;
                    ApplyNumberFormat(cell, format);
                    break;
                case TimeSpan ts:
                    cell.Value = ts;
                    break;
                case Guid g:
                    cell.Value = g.ToString();
                    break;
                case Enum e:
                    cell.Value = e.ToString();
                    break;
                case sbyte:
                case byte:
                case short:
                case ushort:
                case int:
                case uint:
                case long:
                case ulong:
                case float:
                case double:
                case decimal:
                    // 全部数值字面量类型保持数值单元格（XLCellValue.FromObject 按装箱类型还原），Format 映射为数字格式
                    cell.Value = XLCellValue.FromObject(value);
                    ApplyNumberFormat(cell, format);
                    break;
                default:
                    // 转换器列的任意返回类型：IFormattable 按声明格式转文本，其余转字符串
                    if (!string.IsNullOrEmpty(format) && value is IFormattable formattable)
                        cell.Value = formattable.ToString(format, CultureInfo.InvariantCulture);
                    else
                        cell.Value = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                    break;
            }
        }

        private static void ApplyNumberFormat(IXLCell cell, string format)
        {
            if (!string.IsNullOrEmpty(format))
                cell.Style.NumberFormat.Format = format;
        }
    }
}
