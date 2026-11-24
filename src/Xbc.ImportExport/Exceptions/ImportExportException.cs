namespace Xbc.ImportExport
{
    /// <summary>导入导出框架异常基类（所有 ImportExport 异常均派生自此）。</summary>
    public abstract class ImportExportException : Exception
    {
        /// <summary>初始化导入导出框架异常。</summary>
        protected ImportExportException(string message, Exception innerException = null)
            : base(message, innerException) { }
    }
}
