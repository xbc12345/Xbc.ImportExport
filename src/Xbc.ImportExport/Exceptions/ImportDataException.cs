namespace Xbc.ImportExport
{
    /// <summary>导入数据异常（数据行解析失败，携带行号与列名定位坏数据行）。</summary>
    public sealed class ImportDataException : ImportException
    {
        /// <summary>数据行行号（从 1 开始，0 表示未定位到具体数据行）。</summary>
        public int RowNumber { get; }

        /// <summary>列名（string.Empty 表示未设置）。</summary>
        public string ColumnName { get; } = string.Empty;

        /// <summary>初始化导入数据异常（columnName 为 null 时视为 string.Empty）。</summary>
        public ImportDataException(
            string message,
            int rowNumber = 0,
            string columnName = "",
            Exception innerException = null) : base(message, innerException)
        {
            RowNumber = rowNumber;
            ColumnName = columnName ?? string.Empty;
        }
    }
}
