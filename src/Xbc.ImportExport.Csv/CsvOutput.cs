using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Text;

namespace Xbc.ImportExport.Csv
{
    /// <summary>
    /// CSV 写出公共编排（导出、导入模板与错误回执共用）：统一 MemoryStream + UTF-8 StreamWriter + CsvWriter 的
    /// 构造、Flush 与释放时序，写出内容由调用方委托注入；转义与换行符（\r\n）由 CsvWriter 按 RFC 4180 处理。
    /// </summary>
    internal static class CsvOutput
    {
        /// <summary>
        /// 同步写出（模板、错误回执等纯内存构造场景）：委托完成后 Flush 并重置读位置，返回可读流；
        /// Encoding.UTF8 写出带 BOM，保证 Excel 直接打开时中文不乱码。
        /// </summary>
        public static Stream Write(Action<CsvWriter> write)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true);
            var csv = new CsvWriter(writer, CreateConfig());
            var succeeded = false;

            try
            {
                write(csv);
                csv.Flush();
                stream.Position = 0;
                succeeded = true;
                return stream;
            }
            finally
            {
                // 先释放 csv（向底层流 Flush），失败路径再释放流；嵌套资源须 csv 先于 stream，与 WriteAsync 同构
                csv.Dispose();

                if (!succeeded)
                    stream.Dispose();
            }
        }

        /// <summary>异步写出（导出器流式枚举数据源场景）：委托完成后 FlushAsync 并重置读位置，返回可读流。</summary>
        public static async Task<Stream> WriteAsync(Func<CsvWriter, Task> write)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true);
            var csv = new CsvWriter(writer, CreateConfig());
            var succeeded = false;

            try
            {
                await write(csv).ConfigureAwait(false);
                await csv.FlushAsync().ConfigureAwait(false);
                stream.Position = 0;
                succeeded = true;
                return stream;
            }
            finally
            {
                // 先释放 CsvWriter（会向底层流 Flush），失败路径再释放流；
                // 若在 catch 中先行释放流，此处 Flush 将抛 ObjectDisposedException 吞掉包装异常
                csv.Dispose();

                if (!succeeded)
                    stream.Dispose();
            }
        }

        /// <summary>
        /// 统一写出配置：InvariantCulture + 逗号分隔。表头是否写出由调用方逐字段 WriteField 决定，
        /// 不经 CsvHelper 的记录映射写出，故无需设置 HasHeaderRecord。
        /// </summary>
        private static CsvConfiguration CreateConfig()
        {
            return new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ","
            };
        }
    }
}
