namespace Xbc.ImportExport
{
    /// <summary>导入器抽象（文件流 → 实体序列，流式逐行；并生成导入模板供用户填写）。</summary>
    public interface IImporter
    {
        /// <summary>流式导入文件，逐行返回实体（含行号）。</summary>
        /// <typeparam name="T">导入实体类型。</typeparam>
        /// <param name="stream">文件流。</param>
        /// <param name="options">导入选项（含列映射、起始行等）。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步枚举（每项为解析后的行数据与行号）。</returns>
        IAsyncEnumerable<ParsedRow<T>> ImportAsync<T>(
            Stream stream,
            ImportOptions options,
            CancellationToken cancellationToken = default) where T : class, new();

        /// <summary>生成导入模板（表头、必填标记、示例行等，按各格式能力呈现）。</summary>
        /// <typeparam name="T">导入实体类型。</typeparam>
        /// <param name="options">模板选项（示例行、字段说明等；格式不支持的能力自动忽略）。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>模板文件流（位置已重置到开头）。</returns>
        /// <remarks>模板列仅基于实体声明的 <c>[ImportColumn]</c> 特性生成，不支持程序化列定义（导入时的列映射由 <see cref="ImportOptions.Columns"/> 另行指定）。</remarks>
        Task<Stream> GenerateTemplateAsync<T>(
            TemplateOptions options = null,
            CancellationToken cancellationToken = default) where T : class, new();

        /// <summary>生成导入错误回执（仅包含错误行：行号 + 原始列 + 错误信息，按各格式能力呈现，回传填写方修正后重新提交）。</summary>
        /// <typeparam name="T">导入实体类型。</typeparam>
        /// <param name="rows">导入结果行集合（仅其中带错误的行进入回执）。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>回执文件流（位置已重置到开头）。</returns>
        /// <remarks>
        /// 回执表头为行号 + 原始列（错误行 <see cref="ParsedRow{T}.RawValues"/> 键的首次出现顺序）+ 错误信息（同一行多条以分号串联）；
        /// 无错误行时仅输出表头。公式触发文本统一加 ' 前缀防护（CSV 为字面字符，Excel 为 quotePrefix 样式）。
        /// </remarks>
        Task<Stream> GenerateErrorReportAsync<T>(
            IEnumerable<ParsedRow<T>> rows,
            CancellationToken cancellationToken = default) where T : class, new();
    }
}
