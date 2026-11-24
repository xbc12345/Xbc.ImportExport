using Xbc.ImportExport.Csv;
using Xbc.ImportExport.Excel;

namespace Xbc.ImportExport.Tests
{
    public class RequiredImportTests
    {
        private readonly CsvImporter csvImporter = new CsvImporter();
        private readonly ExcelImporter excelImporter = new ExcelImporter();

        [Fact]
        public async Task Import_RequiredMissing_StrictMode_ThrowsWithLocation()
        {
            var content = "编码,名称,备注\n,温度传感器,无";
            var options = new ImportOptions { ErrorMode = ImportErrorMode.Strict };

            var exception = await Assert.ThrowsAsync<ImportDataException>(
                async () => await TestHelpers.ImportAllAsync<RequiredDto>(csvImporter, TestHelpers.ToStream(content), options));

            Assert.Equal(2, exception.RowNumber);
            Assert.Equal("编码", exception.ColumnName);
            Assert.Contains("必填", exception.Message);
        }

        [Fact]
        public async Task Import_RequiredMissing_CollectMode_RecordsError()
        {
            var content = "编码,名称,备注\n,温度传感器,无\nD001,,无";
            var options = new ImportOptions { ErrorMode = ImportErrorMode.Collect };

            var rows = await TestHelpers.ImportAllAsync<RequiredDto>(csvImporter, TestHelpers.ToStream(content), options);

            Assert.Equal(2, rows.Count);
            var error = Assert.Single(rows[0].Errors);
            Assert.Contains("编码", error);
            Assert.Contains("必填", error);
            Assert.Null(rows[0].Data.Code);
            Assert.Equal("温度传感器", rows[0].Data.Name);
            Assert.Empty(rows[1].Errors);
            Assert.Equal("D001", rows[1].Data.Code);
        }

        [Fact]
        public async Task Import_RequiredMissing_LenientMode_FallsBackSilently()
        {
            // 宽容模式静默回退默认值（null），不算拦截，行照常产出
            var content = "编码,名称,备注\n,温度传感器,无";

            var rows = await TestHelpers.ImportAllAsync<RequiredDto>(csvImporter, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Empty(row.Errors);
            Assert.Null(row.Data.Code);
        }

        [Fact]
        public async Task Import_RequiredWithDefaultValue_EmptyCellSatisfiesRequired()
        {
            // 声明默认值的必填列，空单元格应用默认值而非必填缺失
            var content = "编码,名称,备注\nD001,,";

            var rows = await TestHelpers.ImportAllAsync<RequiredDto>(csvImporter, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Empty(row.Errors);
            Assert.Equal("D001", row.Data.Code);
            Assert.Equal("未命名", row.Data.Name);
        }

        [Fact]
        public async Task Import_RequiredFilledCell_PassesNormally()
        {
            var content = "编码,名称,备注\nD001,温度传感器,无";
            var options = new ImportOptions { ErrorMode = ImportErrorMode.Strict };

            var rows = await TestHelpers.ImportAllAsync<RequiredDto>(csvImporter, TestHelpers.ToStream(content), options);

            var row = Assert.Single(rows);
            Assert.Empty(row.Errors);
            Assert.Equal("D001", row.Data.Code);
        }

        [Fact]
        public async Task Import_RequiredMissing_ExcelStrictMode_ThrowsWithLocation()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "编码";
                ws.Cell(1, 2).Value = "名称";
                ws.Cell(1, 3).Value = "备注";
                ws.Cell(2, 1).Value = string.Empty;
                ws.Cell(2, 2).Value = "温度传感器";
                ws.Cell(2, 3).Value = "无";
            });
            var options = new ImportOptions { ErrorMode = ImportErrorMode.Strict };

            var exception = await Assert.ThrowsAsync<ImportDataException>(
                async () => await TestHelpers.ImportAllAsync<RequiredDto>(excelImporter, stream, options));

            Assert.Equal(2, exception.RowNumber);
            Assert.Equal("编码", exception.ColumnName);
        }

        [Fact]
        public async Task Import_RequiredColumnHeaderMissing_CsvThrowsImportException()
        {
            // 表头缺必填列（用错模板或列名被改）属文件结构错误，任何错误模式下快速失败（不归 ErrorMode 管辖）
            var content = "名称,备注\r\n温度传感器,无";
            var options = new ImportOptions { ErrorMode = ImportErrorMode.Lenient };

            var exception = await Assert.ThrowsAsync<ImportException>(
                async () => await TestHelpers.ImportAllAsync<RequiredDto>(csvImporter, TestHelpers.ToStream(content), options));

            Assert.Contains("编码", exception.Message);
            Assert.Contains("表头", exception.Message);
        }

        [Fact]
        public async Task Import_RequiredColumnHeaderMissing_ExcelThrowsImportException()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "名称";
                ws.Cell(1, 2).Value = "备注";
                ws.Cell(2, 1).Value = "温度传感器";
                ws.Cell(2, 2).Value = "无";
            });
            var options = new ImportOptions { ErrorMode = ImportErrorMode.Collect };

            var exception = await Assert.ThrowsAsync<ImportException>(
                async () => await TestHelpers.ImportAllAsync<RequiredDto>(excelImporter, stream, options));

            Assert.Contains("编码", exception.Message);
        }

        [Fact]
        public async Task Import_NonRequiredColumnHeaderMissing_StillSkippedSilently()
        {
            // 非必填列缺表头维持既有缺列跳过语义（表头含必填列"编码"，缺非必填列"备注"）
            var content = "编码,名称\r\nD001,温度传感器";

            var rows = await TestHelpers.ImportAllAsync<RequiredDto>(csvImporter, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Empty(row.Errors);
            Assert.Equal("D001", row.Data.Code);
        }
    }
}
