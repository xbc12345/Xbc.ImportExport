namespace Xbc.ImportExport
{
    /// <summary>导出异常（导出过程失败、序列化错误、目标流不可写）。</summary>
    public sealed class ExportException : ImportExportException
    {
        /// <summary>初始化导出异常。</summary>
        public ExportException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}
