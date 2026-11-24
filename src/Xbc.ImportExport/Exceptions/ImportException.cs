namespace Xbc.ImportExport
{
    /// <summary>导入异常（数据行解析之外的导入失败，如列映射与类型声明错误、流不可用、表头解析失败）。</summary>
    public class ImportException : ImportExportException
    {
        /// <summary>初始化导入异常。</summary>
        public ImportException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}
