namespace Xbc.ImportExport
{
    /// <summary>导入错误回执的公共支持（格式无关的错误行筛选与回执表头构建，供各格式导入器的回执生成共用；公式触发文本防护与回执写出属格式特定逻辑，由各导入器自行实现）。</summary>
    internal static class ErrorReportSupport
    {
        /// <summary>行号列名（回执首列，定位原始文件中的行）。</summary>
        public const string RowNumberColumnName = "行号";

        /// <summary>错误信息列名（回执末列，同一行多条错误以分号串联）。</summary>
        public const string ErrorColumnName = "错误信息";

        /// <summary>
        /// 筛选带错误的行并构建回执表头（行号 + 原始列 + 错误信息）。
        /// <para>原始列表头取错误行 <see cref="ParsedRow{T}.RawValues"/> 键的首次出现顺序；无错误行时表头仅含首末两列。</para>
        /// </summary>
        public static List<ParsedRow<T>> CollectErrorRows<T>(IEnumerable<ParsedRow<T>> rows, out List<string> headers) where T : class, new()
        {
            var errorRows = new List<ParsedRow<T>>();
            foreach (var row in rows)
            {
                if (row != null && row.Errors != null && row.Errors.Count > 0)
                    errorRows.Add(row);
            }

            headers = new List<string> { RowNumberColumnName };
            var seenHeaders = new HashSet<string>();
            foreach (var row in errorRows)
            {
                if (row.RawValues == null)
                    continue;

                foreach (var key in row.RawValues.Keys)
                {
                    if (seenHeaders.Add(key))
                        headers.Add(key);
                }
            }

            headers.Add(ErrorColumnName);
            return errorRows;
        }
    }
}
