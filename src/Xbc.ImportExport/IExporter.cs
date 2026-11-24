namespace Xbc.ImportExport
{
    /// <summary>导出器抽象（实体序列 → 文件流，流式写入）。</summary>
    public interface IExporter
    {
        /// <summary>流式导出文件。</summary>
        /// <typeparam name="T">导出实体类型。</typeparam>
        /// <param name="data">数据源（异步枚举，延迟枚举不触发查询，取消经 WithCancellation 传递）。</param>
        /// <param name="options">导出选项。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>导出的文件流（调用方负责释放）。</returns>
        Task<Stream> ExportAsync<T>(
            IAsyncEnumerable<T> data,
            ExportOptions options,
            CancellationToken cancellationToken = default) where T : class;
    }
}
