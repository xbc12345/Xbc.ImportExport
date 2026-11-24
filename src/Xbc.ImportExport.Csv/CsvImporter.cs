using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Xbc.ImportExport.Csv
{
    /// <summary>CSV 导入器（基于 CsvHelper，流式逐行解析与导入模板生成）。</summary>
    public sealed class CsvImporter : IImporter
    {
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

        private async IAsyncEnumerable<ParsedRow<T>> ImportCoreAsync<T>(
            Stream stream,
            ImportOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken) where T : class, new()
        {
            var effectiveOptions = options ?? new ImportOptions();
            var startRow = effectiveOptions.StartRow > 0 ? effectiveOptions.StartRow : 1;
            var targetType = typeof(T);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                // 不忽略物理空行，空行是否跳过由 SkipEmptyRows 统一决定
                IgnoreBlankLines = false,
                // 截断行（字段数少于表头列数）不抛异常，缺失字段返回 null 走空值默认值路径（对齐 Excel 侧越界取空）
                MissingFieldFound = null
            };

            // 构造即读取流（不可读/已释放的流在此抛出），流不可用属调用方使用错误而非文件数据问题
            StreamReader reader = null;
            CsvReader csv;
            try
            {
                // 固定 UTF-8 读取（StreamReader 默认识别 UTF-8/UTF-16 BOM）；其他编码由调用方预先转码。
                // leaveOpen：不接管调用方流（与 ExcelImporter 及导出器的流语义对称，消费方提前中断枚举后流仍可用）
                reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, 1024, leaveOpen: true);
                csv = new CsvReader(reader, config);
            }
            catch (Exception ex)
            {
                reader?.Dispose();
                throw new ImportException($"CSV 流打开失败：{ex.Message}", ex);
            }

            using (reader)
            using (csv)
            {
                string[] headerRecord = null;
                var earlyExit = false;

                try
                {
                    // 跳过前 startRow - 1 行，确保 csv.HeaderRecord 为 startRow 行
                    for (var i = 1; i < startRow; i++)
                    {
                        if (!await csv.ReadAsync().ConfigureAwait(false))
                        {
                            earlyExit = true;
                            break;
                        }
                    }

                    if (!earlyExit)
                    {
                        // 读取表头
                        if (!await csv.ReadAsync().ConfigureAwait(false))
                        {
                            earlyExit = true;
                        }
                        else
                        {
                            csv.ReadHeader();
                            headerRecord = csv.HeaderRecord;

                            if (headerRecord == null)
                                earlyExit = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new ImportException($"CSV 表头解析失败：{ex.Message}", ex);
                }

                // 表头是导入的先决契约：表头行读不到（文件行数不足起始行）属文件结构错误，快速失败
                if (earlyExit)
                    throw new ImportException($"第 {startRow} 行无有效表头（文件为空或起始行超出数据范围）");

                var rowNumber = startRow;
                var headerMap = BuildHeaderMap(headerRecord);
                if (headerMap.Count == 0)
                    throw new ImportException($"第 {startRow} 行无有效表头（表头列名全为空白）");

                // 构建列映射（列名 → 列索引 + PropertyInfo；程序化列优先）
                var propMappings = ImportColumnResolver.BuildMapping(
                    targetType,
                    effectiveOptions.Columns,
                    headerMap);

                // 数据行计数（跳过的空行不计），小于等于 0 表示不限（默认 100000 防 OOM 由选项保证）
                var maxRows = effectiveOptions.MaxRows > 0 ? effectiveOptions.MaxRows : int.MaxValue;
                var dataRowCount = 0;

                while (true)
                {
                    ParsedRow<T> row;
                    try
                    {
                        if (!await csv.ReadAsync().ConfigureAwait(false))
                            break;

                        cancellationToken.ThrowIfCancellationRequested();
                        rowNumber++;

                        // 整行字段均为空白视为空行（含未映射列），按选项决定跳过还是产出默认值行
                        var record = csv.Parser.Record;
                        var isBlankLine = true;
                        for (var i = 0; i < record.Length; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(record[i]))
                            {
                                isBlankLine = false;
                                break;
                            }
                        }

                        // 空行是否跳过由 SkipEmptyRows 统一决定
                        if (isBlankLine && effectiveOptions.SkipEmptyRows)
                            continue;

                        // 数据行超限按数据异常定位行号中断（跳过的空行不计入）
                        dataRowCount++;
                        if (dataRowCount > maxRows)
                            throw new ImportDataException($"导入行数超过上限 {maxRows}", rowNumber, "");

                        var entity = new T();
                        var rawValues = new Dictionary<string, string>();
                        var errors = new List<string>();

                        foreach (var mapping in propMappings)
                        {
                            var fieldValue = csv.GetField(mapping.ColumnIndex) ?? string.Empty;
                            rawValues[mapping.Column.Name] = fieldValue;

                            // 按错误模式解析列值（宽容回退、严格抛出、收集记录）
                            var converted = ImportValueParser.Resolve(
                                mapping, fieldValue, effectiveOptions.ErrorMode, rowNumber, errors);
                            if (converted != null)
                                mapping.Column.Property.SetValue(entity, converted);
                        }

                        row = new ParsedRow<T>
                        {
                            RowNumber = rowNumber,
                            Data = entity,
                            RawValues = rawValues,
                            Errors = errors
                        };
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException && ex is not ImportExportException)
                    {
                        throw new ImportDataException($"CSV 导入失败：{ex.Message}", rowNumber, "", ex);
                    }

                    // 注意：yield 不能出现在带 catch 的 try 块内，故解析在 try-catch 内完成
                    yield return row;
                }
            }
        }

        /// <summary>从已解析的表头记录构建列名到列索引的映射（忽略空白列名，大小写不敏感，重复列名属文件结构错误快速失败）。</summary>
        private static Dictionary<string, int> BuildHeaderMap(string[] headerRecord)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headerRecord.Length; i++)
            {
                var header = headerRecord[i]?.Trim();
                if (string.IsNullOrEmpty(header))
                    continue;

                // 重复列名（大小写不敏感）无法建立唯一列映射，快速失败而非静默取最后一列
                if (map.ContainsKey(header))
                    throw new ImportException($"表头列名 '{header}' 在第 {i + 1} 列重复出现，无法建立唯一的列映射");

                map[header] = i;
            }

            return map;
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

            // CSV 模板为纯文本（表头 + 可选示例行）；列宽、数据校验、字段说明与工作表名不适用，按格式忽略；
            // 示例行无格式标记能力（Excel 侧为灰色斜体 + 批注警示），使用方需自行提示用户导入前删除示例行
            // 转义与换行符（\r\n）由 CsvWriter 按 RFC 4180 处理，与 CsvExporter 同一写出实现（CsvOutput 统一编排）
            try
            {
                return Task.FromResult(CsvOutput.Write(csv =>
                {
                    // 写表头
                    foreach (var column in columns)
                    {
                        csv.WriteField(column.Name);
                    }

                    csv.NextRecord();

                    // 可选示例行
                    if (effectiveOptions.IncludeExampleRows)
                    {
                        foreach (var column in columns)
                        {
                            csv.WriteField(column.GetExampleValue());
                        }

                        csv.NextRecord();
                    }
                }));
            }
            catch (Exception ex) when (ex is not OperationCanceledException && ex is not ImportExportException)
            {
                throw new TemplateException($"CSV 模板生成失败：{ex.Message}", ex);
            }
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

            // 回执为 RFC 4180 文本（UTF-8 带 BOM，Excel 直接打开时中文不乱码；转义与换行符由 CsvWriter 处理，与模板/导出同一写出实现）
            return Task.FromResult(CsvOutput.Write(csv =>
            {
                // 写表头
                foreach (var header in headers)
                {
                    csv.WriteField(header);
                }

                csv.NextRecord();

                // 写错误行
                foreach (var row in errorRows)
                {
                    // 行号列（数字，直接定位原始文件中的行）
                    csv.WriteField(row.RowNumber);

                    // 原始列按表头取值，缺键（如程序化列名与表头不一致）输出空串
                    for (var i = 1; i < headers.Count - 1; i++)
                    {
                        row.RawValues.TryGetValue(headers[i], out var rawValue);
                        csv.WriteField(CsvFormulaProtection.EscapeFormula(rawValue ?? string.Empty));
                    }

                    csv.WriteField(string.Join("；", row.Errors));
                    csv.NextRecord();
                }
            }));
        }
    }
}
