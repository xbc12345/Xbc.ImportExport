using ClosedXML.Excel;
using Xbc.ImportExport.Excel;

namespace Xbc.ImportExport.Tests
{
    /// <summary>
    /// Excel 类型化单元格的导入文本化行为：数字/布尔/日期/时长单元格按 InvariantCulture 转文本
    /// （GetCellText 类型分支），与声明 Format 的日期列对齐；错误回执行号为数字单元格。
    /// </summary>
    public class ExcelTypedCellImportTests
    {
        private readonly ExcelImporter importer = new ExcelImporter();
        private readonly ExcelImporter reportImporter = new ExcelImporter();

        [Fact]
        public async Task Import_TypedNumberCell_ParsesViaInvariantText()
        {
            // 数字单元格 1.5 → "1.5" → double 属性（非 CurrentCulture 的 "1,5"，任何文化下均可解析）
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "价格";
                ws.Cell(2, 1).Value = 1.5;
            });

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream);

            var row = Assert.Single(rows);
            Assert.Equal(1.5, row.Data.Price);
        }

        [Fact]
        public async Task Import_TypedBooleanCell_Parses()
        {
            // 布尔单元格 → "True"/"False" 文本 → TryParseBoolean 大小写不敏感
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "启用";
                ws.Cell(2, 1).Value = true;
                ws.Cell(3, 1).Value = false;
            });

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream);

            Assert.Equal(2, rows.Count);
            Assert.True(rows[0].Data.Enabled);
            Assert.False(rows[1].Data.Enabled);
        }

        [Fact]
        public async Task Import_TypedDateTimeCell_WithDeclaredFormat_ParsesExact()
        {
            // 日期单元格按列声明 Format 文本化（yyyy-MM-dd），与解析侧 TryParseExact 对齐往返
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "日期";
                ws.Cell(2, 1).Value = new DateTime(2026, 1, 2);
            });
            var options = new ImportOptions
            {
                Columns =
                {
                    new ImportColumn { PropertyName = nameof(FormattedDto.Day), Property = typeof(FormattedDto).GetProperty(nameof(FormattedDto.Day)), Name = "日期", Format = "yyyy-MM-dd" }
                }
            };

            var rows = await TestHelpers.ImportAllAsync<FormattedDto>(importer, stream, options);

            var row = Assert.Single(rows);
            Assert.Equal(new DateTime(2026, 1, 2), row.Data.Day);
        }

        [Fact]
        public async Task Import_TypedDateTimeCell_WithoutFormat_ParsesInvariant()
        {
            // 未声明 Format 的日期单元格按 InvariantCulture 默认文本解析
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "生产日期";
                ws.Cell(2, 1).Value = new DateTime(2026, 1, 2, 3, 4, 5);
            });

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream);

            var row = Assert.Single(rows);
            Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), row.Data.MadeAt);
        }

        [Fact]
        public async Task GenerateErrorReport_RowNumberCell_IsNumericForSorting()
        {
            // 回执行号写为数字单元格（回执内可直接排序），非文本
            var rows = new[]
            {
                new ParsedRow<SimpleDto>
                {
                    RowNumber = 7,
                    Data = new SimpleDto { Code = "D001" },
                    RawValues = new Dictionary<string, string> { ["设备编码"] = "D001" },
                    Errors = new List<string> { "编码不存在" }
                }
            };

            var stream = await reportImporter.GenerateErrorReportAsync(rows);

            using (var workbook = new XLWorkbook(stream))
            {
                var cell = workbook.Worksheets.First().Cell(2, 1);
                Assert.Equal(XLDataType.Number, cell.DataType);
                Assert.Equal(7, cell.Value.GetNumber());
            }
        }
    }
}
