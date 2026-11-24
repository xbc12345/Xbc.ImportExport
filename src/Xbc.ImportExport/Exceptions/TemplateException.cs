namespace Xbc.ImportExport
{
    /// <summary>模板异常（模板不存在、模板格式错误、模板字段映射缺失）。</summary>
    public sealed class TemplateException : ImportExportException
    {
        /// <summary>初始化模板异常。</summary>
        public TemplateException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}
