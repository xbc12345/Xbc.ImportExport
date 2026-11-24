using ClosedXML.Excel;
using System.Runtime.CompilerServices;
using System.Text;

namespace Xbc.ImportExport.Tests
{
    /// <summary>测试辅助方法（流构建、异步枚举物化、数据源委托）。</summary>
    internal static class TestHelpers
    {
        /// <summary>将字符串内容转为 UTF-8 内存流。</summary>
        public static MemoryStream ToStream(string content)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(content));
        }

        /// <summary>构造单工作表（Sheet1）xlsx 内存流。</summary>
        public static MemoryStream BuildWorksheet(Action<IXLWorksheet> fill)
        {
            return BuildWorkbook(workbook => fill(workbook.Worksheets.Add("Sheet1")));
        }

        /// <summary>构造多工作表 xlsx 内存流（工作表命名与填充由委托完成）。</summary>
        public static MemoryStream BuildWorkbook(Action<IXLWorkbook> fill)
        {
            var workbook = new XLWorkbook();
            fill(workbook);
            var stream = new MemoryStream();
            workbook.SaveAs(stream);
            workbook.Dispose();
            stream.Position = 0;
            return stream;
        }

        /// <summary>读取流全部内容为字符串。</summary>
        public static string ReadAll(Stream stream)
        {
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>物化导入器的异步行序列。</summary>
        public static async Task<List<ParsedRow<T>>> ImportAllAsync<T>(
            IImporter importer,
            Stream stream,
            ImportOptions options = null,
            CancellationToken cancellationToken = default) where T : class, new()
        {
            var rows = new List<ParsedRow<T>>();
            await foreach (var row in importer.ImportAsync<T>(stream, options ?? new ImportOptions(), cancellationToken).ConfigureAwait(false))
            {
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>构造导出器的数据源（逐项枚举，响应取消）。</summary>
        public static IAsyncEnumerable<T> DataOf<T>(params T[] items)
        {
            return ItemsAsync(items, CancellationToken.None);
        }

        /// <summary>构造枚举即抛出指定异常的数据源（验证导出器的异常包装契约）。</summary>
        public static IAsyncEnumerable<T> DataOfThrowing<T>(Exception error) where T : class
        {
            return ThrowingAsync<T>(error, CancellationToken.None);
        }

#pragma warning disable CS1998 // async 修饰符为 IAsyncEnumerable 所需；无真实 await
        private static async IAsyncEnumerable<T> ItemsAsync<T>(IEnumerable<T> items, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
            }
        }
#pragma warning restore CS1998

#pragma warning disable CS1998 // async 修饰符为 IAsyncEnumerable 所需；无真实 await
        private static async IAsyncEnumerable<T> ThrowingAsync<T>(Exception error, [EnumeratorCancellation] CancellationToken cancellationToken) where T : class
        {
            cancellationToken.ThrowIfCancellationRequested();
            // async 迭代器要求至少一个 yield；导出器会跳过 null 项，随后的枚举抛出异常
            yield return null;
            throw error;
        }
#pragma warning restore CS1998
    }
}
