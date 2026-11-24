using CsvHelper;
using System.Globalization;

namespace Xbc.ImportExport.Csv
{
    /// <summary>CSV 导出器（基于 CsvHelper，流式写入 CSV）。</summary>
    public sealed class CsvExporter : IExporter
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

            // 资源构造、Flush 与释放时序由 CsvOutput 统一编排，写出内容以委托注入
            try
            {
                return await CsvOutput.WriteAsync(async csv =>
                {
                    // 写表头
                    if (effectiveOptions.IncludeHeader)
                    {
                        foreach (var column in columns)
                        {
                            csv.WriteField(column.Name);
                        }

                        csv.NextRecord();
                    }

                    // 写数据行
                    var rowCount = 0;
                    // 小于等于 0 表示不限（默认 100000 防 OOM 由选项保证）
                    var maxRows = effectiveOptions.MaxRows > 0 ? effectiveOptions.MaxRows : int.MaxValue;

                    await foreach (var item in data.WithCancellation(cancellationToken).ConfigureAwait(false))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (item == null)
                            continue;

                        if (rowCount >= maxRows)
                            throw new ExportException($"导出行数超过上限 {maxRows}");

                        WriteRow(csv, item, columns);
                        rowCount++;
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException && ex is not ImportExportException)
            {
                throw new ExportException($"CSV 导出失败：{ex.Message}", ex);
            }
        }

        private static void WriteRow<T>(CsvWriter csv, T item, List<ExportColumn> columns)
        {
            foreach (var column in columns)
            {
                var value = column.Property.GetValue(item);
                if (value == null)
                {
                    csv.WriteField(string.Empty);
                    continue;
                }

                // 值转换器优先
                if (column.Converter != null)
                    value = column.Converter(value);

                csv.WriteField(FormatValue(value, column.Format));
            }

            csv.NextRecord();
        }

        private static string FormatValue(object value, string format)
        {
            if (value == null)
                return string.Empty;

            // 字符串列的公式触发文本加 ' 前缀（Excel 打开 CSV 时不解释为公式；数值列负号不经过此分支，保持裸文本）
            if (value is string s)
                return CsvFormulaProtection.EscapeFormula(s);

            if (!string.IsNullOrEmpty(format) && value is IFormattable formattable)
                return formattable.ToString(format, CultureInfo.InvariantCulture);

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }
    }
}
