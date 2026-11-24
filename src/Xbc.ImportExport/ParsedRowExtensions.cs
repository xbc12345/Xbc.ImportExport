using System.Runtime.CompilerServices;

namespace Xbc.ImportExport
{
    /// <summary>ParsedRow 流装饰扩展（业务校验等横切处理并入导入管线，导入器零改动）。</summary>
    public static class ParsedRowExtensions
    {
        /// <summary>
        /// 为导入行附加业务校验（装饰器：逐行调用校验委托，错误信息并入 <see cref="ParsedRow{T}.Errors"/>，不拦截行产出，由调用方按 Errors 决定取舍）。
        /// </summary>
        /// <typeparam name="T">导入实体类型。</typeparam>
        /// <param name="source">导入行流（通常为 <c>Importer.ImportAsync</c> 的返回值）。</param>
        /// <param name="validator">业务校验委托（入参为解析后的实体，返回单条错误信息；返回 null 或空串视为通过，多条规则链式多个 ValidateWith 或自行拼接）。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>带业务校验结果的导入行流。</returns>
        public static async IAsyncEnumerable<ParsedRow<T>> ValidateWith<T>(
            this IAsyncEnumerable<ParsedRow<T>> source,
            Func<T, string> validator,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
            where T : class, new()
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (validator == null)
                throw new ArgumentNullException(nameof(validator));

            await foreach (var row in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                // 空行产物（无 Data）与解析失败行不参与业务校验，原样透传
                if (row != null && row.Data != null)
                {
                    var error = validator(row.Data);
                    if (!string.IsNullOrEmpty(error))
                        row.Errors.Add(error);
                }

                yield return row;
            }
        }
    }
}
