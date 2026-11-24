using Xbc.ImportExport.Csv;
using Xbc.ImportExport.Excel;

namespace Xbc.ImportExport.Tests
{
    public class MaxRowsImportTests
    {
        private readonly CsvImporter csvImporter = new CsvImporter();
        private readonly ExcelImporter excelImporter = new ExcelImporter();

        [Fact]
        public async Task Import_RowsExceedMaxRows_CsvThrowsWithDataLocation()
        {
            var content = "设备编码,设备名称\r\nD001,a\r\nD002,b\r\nD003,c";
            var options = new ImportOptions { MaxRows = 2 };

            var exception = await Assert.ThrowsAsync<ImportDataException>(
                async () => await TestHelpers.ImportAllAsync<SimpleDto>(csvImporter, TestHelpers.ToStream(content), options));

            Assert.Equal(4, exception.RowNumber);
            Assert.Contains("上限", exception.Message);
        }

        [Fact]
        public async Task Import_RowsExactlyAtMaxRows_CsvPasses()
        {
            var content = "设备编码,设备名称\r\nD001,a\r\nD002,b";
            var options = new ImportOptions { MaxRows = 2 };

            var rows = await TestHelpers.ImportAllAsync<SimpleDto>(csvImporter, TestHelpers.ToStream(content), options);

            Assert.Equal(2, rows.Count);
        }

        [Fact]
        public async Task Import_MaxRowsZeroOrNegative_MeansUnlimited()
        {
            var content = "设备编码,设备名称\r\nD001,a\r\nD002,b\r\nD003,c";
            var options = new ImportOptions { MaxRows = 0 };

            var rows = await TestHelpers.ImportAllAsync<SimpleDto>(csvImporter, TestHelpers.ToStream(content), options);

            Assert.Equal(3, rows.Count);
        }

        [Fact]
        public async Task Import_SkippedEmptyRows_NotCountedTowardMaxRows()
        {
            // 空行跳过不计数：两行数据 + 中间空行在 MaxRows = 2 下不超限
            var content = "设备编码,设备名称\r\nD001,a\r\n\r\nD002,b";
            var options = new ImportOptions { MaxRows = 2 };

            var rows = await TestHelpers.ImportAllAsync<SimpleDto>(csvImporter, TestHelpers.ToStream(content), options);

            Assert.Equal(2, rows.Count);
            Assert.Equal("D002", rows[1].Data.Code);
        }

        [Fact]
        public async Task Import_RowsExceedMaxRows_ExcelThrowsWithDataLocation()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "设备编码";
                ws.Cell(1, 2).Value = "设备名称";
                ws.Cell(2, 1).Value = "D001";
                ws.Cell(2, 2).Value = "a";
                ws.Cell(3, 1).Value = "D002";
                ws.Cell(3, 2).Value = "b";
                ws.Cell(4, 1).Value = "D003";
                ws.Cell(4, 2).Value = "c";
            });
            var options = new ImportOptions { MaxRows = 2 };

            var exception = await Assert.ThrowsAsync<ImportDataException>(
                async () => await TestHelpers.ImportAllAsync<SimpleDto>(excelImporter, stream, options));

            Assert.Equal(4, exception.RowNumber);
            Assert.Contains("上限", exception.Message);
        }
    }
}
