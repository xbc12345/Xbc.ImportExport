using ClosedXML.Excel;
using Xbc.ImportExport.Excel;

namespace Xbc.ImportExport.Tests
{
    public class ExcelErrorReportTests
    {
        private readonly ExcelImporter importer = new ExcelImporter();

        private static ParsedRow<DeviceDto> CreateErrorRow(
            int rowNumber,
            Dictionary<string, string> rawValues,
            params string[] errors)
        {
            return new ParsedRow<DeviceDto>
            {
                RowNumber = rowNumber,
                Data = new DeviceDto(),
                RawValues = rawValues ?? new Dictionary<string, string>(),
                Errors = errors.ToList()
            };
        }

        [Fact]
        public async Task GenerateErrorReport_ErrorRow_HasHighlightAndRedErrorText()
        {
            var rows = new List<ParsedRow<DeviceDto>>
            {
                CreateErrorRow(2, new Dictionary<string, string> { { "设备编码", "D001" } }, "状态解析失败")
            };

            using (var workbook = new XLWorkbook(await importer.GenerateErrorReportAsync(rows)))
            {
                var ws = workbook.Worksheet("错误回执");

                // 表头：行号 + 原始列（RawValues 键首次出现顺序）+ 错误信息
                Assert.Equal("行号", ws.Cell(1, 1).GetString());
                Assert.Equal("设备编码", ws.Cell(1, 2).GetString());
                Assert.Equal("错误信息", ws.Cell(1, 3).GetString());

                // 行号为数字单元格，原始列与错误信息为文本
                Assert.Equal(2, ws.Cell(2, 1).Value.GetNumber());
                Assert.Equal("D001", ws.Cell(2, 2).GetString());
                Assert.Equal("状态解析失败", ws.Cell(2, 3).GetString());

                // 错误行整行浅黄底，错误信息列红字
                Assert.Equal(XLColor.LightYellow, ws.Cell(2, 1).Style.Fill.BackgroundColor);
                Assert.Equal(XLColor.LightYellow, ws.Cell(2, 3).Style.Fill.BackgroundColor);
                Assert.Equal(XLColor.Red, ws.Cell(2, 3).Style.Font.FontColor);

                // 仅表头与错误行，无多余数据行
                Assert.True(ws.Cell(3, 1).IsEmpty());
            }
        }

        [Fact]
        public async Task GenerateErrorReport_CleanRowsExcluded_OnlyErrorRowsWritten()
        {
            var rows = new List<ParsedRow<DeviceDto>>
            {
                new ParsedRow<DeviceDto>
                {
                    RowNumber = 2,
                    Data = new DeviceDto(),
                    RawValues = new Dictionary<string, string> { { "设备编码", "D002" } },
                    Errors = new List<string>()
                },
                CreateErrorRow(3, new Dictionary<string, string> { { "设备编码", "D003" } }, "数量解析失败")
            };

            using (var workbook = new XLWorkbook(await importer.GenerateErrorReportAsync(rows)))
            {
                var ws = workbook.Worksheet("错误回执");

                // 干净行不进入回执，仅第 3 行错误数据出现在第 2 行
                Assert.Equal("D003", ws.Cell(2, 2).GetString());
                Assert.DoesNotContain("D002", ws.Cell(2, 2).GetString());
                Assert.True(ws.Cell(3, 1).IsEmpty());
            }
        }

        [Fact]
        public async Task GenerateErrorReport_MultipleErrors_JoinedWithSemicolon()
        {
            var rows = new List<ParsedRow<DeviceDto>>
            {
                CreateErrorRow(3, new Dictionary<string, string>(), "错误一", "错误二")
            };

            using (var workbook = new XLWorkbook(await importer.GenerateErrorReportAsync(rows)))
            {
                var ws = workbook.Worksheet("错误回执");
                Assert.Equal("错误一；错误二", ws.Cell(2, 2).GetString());
            }
        }

        [Fact]
        public async Task GenerateErrorReport_FormulaTriggerText_StoredAsTextNotFormula()
        {
            // 原始数据为用户输入：公式触发文本加 ' 前缀（ClosedXML 转为 quotePrefix 样式 + 纯文本存储），Excel 打开不解释为公式
            var rows = new List<ParsedRow<DeviceDto>>
            {
                CreateErrorRow(2, new Dictionary<string, string> { { "设备编码", "=SUM(A1)" } }, "状态解析失败")
            };

            using (var workbook = new XLWorkbook(await importer.GenerateErrorReportAsync(rows)))
            {
                var ws = workbook.Worksheet("错误回执");
                var cell = ws.Cell(2, 2);

                Assert.False(cell.HasFormula);
                Assert.Equal("=SUM(A1)", cell.GetText());
                Assert.True(cell.Style.IncludeQuotePrefix);
            }
        }

        [Fact]
        public async Task GenerateErrorReport_MissingRawKey_OutputsEmptyCell()
        {
            // 程序化列名与表头不一致等缺键场景，原始列输出空单元格不抛异常
            var rows = new List<ParsedRow<DeviceDto>>
            {
                CreateErrorRow(2, new Dictionary<string, string> { { "设备编码", "D001" } }, "错误")
            };

            using (var workbook = new XLWorkbook(await importer.GenerateErrorReportAsync(rows)))
            {
                var ws = workbook.Worksheet("错误回执");
                Assert.Equal("D001", ws.Cell(2, 2).GetString());
                Assert.Equal("错误", ws.Cell(2, 3).GetString());
            }
        }

        [Fact]
        public async Task GenerateErrorReport_NoErrorRows_HeaderOnly()
        {
            var rows = new List<ParsedRow<DeviceDto>>
            {
                new ParsedRow<DeviceDto>
                {
                    RowNumber = 2,
                    Data = new DeviceDto(),
                    RawValues = new Dictionary<string, string> { { "设备编码", "D001" } },
                    Errors = new List<string>()
                }
            };

            using (var workbook = new XLWorkbook(await importer.GenerateErrorReportAsync(rows)))
            {
                var ws = workbook.Worksheet("错误回执");

                // 无错误行时仅输出表头（无原始列）
                Assert.Equal("行号", ws.Cell(1, 1).GetString());
                Assert.Equal("错误信息", ws.Cell(1, 2).GetString());
                Assert.True(ws.Cell(2, 1).IsEmpty());
            }
        }

        [Fact]
        public async Task GenerateErrorReport_NullRows_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => importer.GenerateErrorReportAsync<DeviceDto>(null));
        }
    }
}
