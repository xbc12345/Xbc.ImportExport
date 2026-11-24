using Xbc.ImportExport.Csv;
using Xbc.ImportExport.Excel;

namespace Xbc.ImportExport.Tests
{
    public class SheetNameImportTests
    {
        private readonly ExcelImporter excelImporter = new ExcelImporter();
        private readonly CsvImporter csvImporter = new CsvImporter();

        private static MemoryStream BuildTwoSheetWorkbook()
        {
            return TestHelpers.BuildWorkbook(workbook =>
            {
                var first = workbook.Worksheets.Add("说明");
                first.Cell(1, 1).Value = "此表为填表说明";
                var data = workbook.Worksheets.Add("数据表");
                data.Cell(1, 1).Value = "设备编码";
                data.Cell(1, 2).Value = "设备名称";
                data.Cell(2, 1).Value = "D001";
                data.Cell(2, 2).Value = "温度传感器";
            });
        }

        [Fact]
        public async Task Import_SheetNameSelects_WorksheetByName()
        {
            var options = new ImportOptions { SheetName = "数据表" };

            var rows = await TestHelpers.ImportAllAsync<SimpleDto>(excelImporter, BuildTwoSheetWorkbook(), options);

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
            Assert.Equal("温度传感器", row.Data.Name);
        }

        [Fact]
        public async Task Import_SheetNameMissing_ThrowsWithAvailableNames()
        {
            var options = new ImportOptions { SheetName = "不存在" };

            var exception = await Assert.ThrowsAsync<ImportException>(
                async () => await TestHelpers.ImportAllAsync<SimpleDto>(excelImporter, BuildTwoSheetWorkbook(), options));

            Assert.Contains("不存在", exception.Message);
            Assert.Contains("说明", exception.Message);
            Assert.Contains("数据表", exception.Message);
        }

        [Fact]
        public async Task Import_SheetNameNull_SelectsFirstWorksheet()
        {
            // 默认（null）取第一个工作表（"说明"表）：表头无匹配列且无数据行，产出空序列
            var options = new ImportOptions { SheetName = null };

            var rows = await TestHelpers.ImportAllAsync<SimpleDto>(excelImporter, BuildTwoSheetWorkbook(), options);

            Assert.Empty(rows);
        }

        [Fact]
        public async Task Import_SheetNameIgnored_CsvImportsNormally()
        {
            // CSV 无工作表概念，SheetName 被忽略不影响导入
            var content = "设备编码,设备名称\r\nD001,温度传感器";
            var options = new ImportOptions { SheetName = "任意名称" };

            var rows = await TestHelpers.ImportAllAsync<SimpleDto>(csvImporter, TestHelpers.ToStream(content), options);

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
        }
    }
}
