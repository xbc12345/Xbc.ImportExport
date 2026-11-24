using ClosedXML.Excel;
using Xbc.ImportExport.Csv;
using Xbc.ImportExport.Excel;

namespace Xbc.ImportExport.Tests
{
    public class MaxRowsPerSheetTests
    {
        private readonly ExcelExporter excelExporter = new ExcelExporter();
        private readonly CsvExporter csvExporter = new CsvExporter();

        private static IAsyncEnumerable<OrderlessDto> BuildItems(int count)
        {
            var items = new List<OrderlessDto>();
            for (var i = 1; i <= count; i++)
            {
                items.Add(new OrderlessDto { A = "a" + i, B = "b" + i, C = "c" + i });
            }
            return TestHelpers.DataOf(items.ToArray());
        }

        [Fact]
        public async Task Export_RowsExceedPerSheetLimit_SplitsIntoSheets()
        {
            // 5 行数据、每表 2 行 → 3 表（2/2/1），命名 基名/基名-2/基名-3，每表重复表头
            var options = new ExportOptions { SheetName = "数据", MaxRowsPerSheet = 2 };

            var stream = await excelExporter.ExportAsync(BuildItems(5), options);
            using (var workbook = new XLWorkbook(stream))
            {
                Assert.Equal(3, workbook.Worksheets.Count);
                Assert.Equal("数据", workbook.Worksheets.ElementAt(0).Name);
                Assert.Equal("数据-2", workbook.Worksheets.ElementAt(1).Name);
                Assert.Equal("数据-3", workbook.Worksheets.ElementAt(2).Name);

                Assert.Equal(3, workbook.Worksheets.ElementAt(0).LastRowUsed().RowNumber());
                Assert.Equal(3, workbook.Worksheets.ElementAt(1).LastRowUsed().RowNumber());
                Assert.Equal(2, workbook.Worksheets.ElementAt(2).LastRowUsed().RowNumber());

                // 每表表头重复（加粗首行）
                for (var i = 0; i < 3; i++)
                {
                    Assert.Equal("甲", workbook.Worksheets.ElementAt(i).Cell(1, 1).Value.GetText());
                }

                // 尾表数据值
                Assert.Equal("a5", workbook.Worksheets.ElementAt(2).Cell(2, 1).Value.GetText());
            }
        }

        [Fact]
        public async Task Export_MaxRowsPerSheetZero_SingleSheet()
        {
            var options = new ExportOptions { SheetName = "数据", MaxRowsPerSheet = 0 };

            var stream = await excelExporter.ExportAsync(BuildItems(5), options);
            using (var workbook = new XLWorkbook(stream))
            {
                Assert.Single(workbook.Worksheets);
                Assert.Equal(6, workbook.Worksheets.ElementAt(0).LastRowUsed().RowNumber());
            }
        }

        [Fact]
        public async Task Export_SplitWithoutHeader_WritesDataFromFirstRow()
        {
            // IncludeHeader = false 分片时各表无表头，数据从首行写起
            var options = new ExportOptions { SheetName = "数据", MaxRowsPerSheet = 2, IncludeHeader = false };

            var stream = await excelExporter.ExportAsync(BuildItems(3), options);
            using (var workbook = new XLWorkbook(stream))
            {
                Assert.Equal(2, workbook.Worksheets.Count);
                Assert.Equal("a1", workbook.Worksheets.ElementAt(0).Cell(1, 1).Value.GetText());
                Assert.Equal("a2", workbook.Worksheets.ElementAt(0).Cell(2, 1).Value.GetText());
                Assert.Equal("a3", workbook.Worksheets.ElementAt(1).Cell(1, 1).Value.GetText());
            }
        }

        [Fact]
        public async Task Export_MaxRowsPerSheetIgnored_CsvWritesSingleFile()
        {
            // CSV 无工作表概念，MaxRowsPerSheet 被忽略（单文件全部行）
            var options = new ExportOptions { MaxRowsPerSheet = 2 };

            var stream = await csvExporter.ExportAsync(BuildItems(5), options);

            var content = TestHelpers.ReadAll(stream);
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(6, lines.Length);
            Assert.Equal("a5,b5,c5", lines[5]);
        }

        [Fact]
        public async Task Export_SplitSheetNameExceeds31Chars_Throws()
        {
            // 基名 30 字符合法，分片拼接 "-2" 后 32 字符超上限，前置校验抛中文错误而非依赖底层库英文异常
            var options = new ExportOptions { SheetName = new string('数', 30), MaxRowsPerSheet = 1 };

            var exception = await Assert.ThrowsAsync<ExportException>(
                () => excelExporter.ExportAsync(BuildItems(2), options));

            Assert.Contains("31", exception.Message);
        }

        [Fact]
        public async Task Export_BaseSheetNameExceeds31Chars_Throws()
        {
            var options = new ExportOptions { SheetName = new string('数', 32) };

            var exception = await Assert.ThrowsAsync<ExportException>(
                () => excelExporter.ExportAsync(BuildItems(1), options));

            Assert.Contains("31", exception.Message);
        }
    }
}
