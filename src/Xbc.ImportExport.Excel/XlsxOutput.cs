using ClosedXML.Excel;

namespace Xbc.ImportExport.Excel
{
    /// <summary>
    /// Excel 写出公共编排（导出、导入模板与错误回执共用）：统一 XLWorkbook 构造、保存到内存流与释放时序，
    /// 工作簿内容由调用方委托注入。
    /// </summary>
    internal static class XlsxOutput
    {
        /// <summary>同步写出（模板、错误回执等纯内存构造场景）：委托完成后保存到内存流并重置读位置，返回可读流。</summary>
        public static Stream Write(Action<XLWorkbook> write)
        {
            var workbook = new XLWorkbook();
            var stream = new MemoryStream();
            var succeeded = false;

            try
            {
                write(workbook);
                workbook.SaveAs(stream);
                stream.Position = 0;
                succeeded = true;
                return stream;
            }
            finally
            {
                // 成功外的全部路径统一释放流，与 CsvOutput 同构
                workbook.Dispose();

                if (!succeeded)
                    stream.Dispose();
            }
        }

        /// <summary>异步写出（导出器流式枚举数据源场景）：委托完成后保存到内存流并重置读位置，返回可读流。</summary>
        public static async Task<Stream> WriteAsync(Func<XLWorkbook, Task> write)
        {
            var workbook = new XLWorkbook();
            var stream = new MemoryStream();
            var succeeded = false;

            try
            {
                await write(workbook).ConfigureAwait(false);
                workbook.SaveAs(stream);
                stream.Position = 0;
                succeeded = true;
                return stream;
            }
            finally
            {
                workbook.Dispose();

                if (!succeeded)
                    stream.Dispose();
            }
        }
    }
}
