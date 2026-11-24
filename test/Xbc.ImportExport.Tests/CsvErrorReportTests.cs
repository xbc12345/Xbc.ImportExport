using Xbc.ImportExport.Csv;

namespace Xbc.ImportExport.Tests
{
    public class CsvErrorReportTests
    {
        private static readonly CsvImporter Importer = new CsvImporter();

        private static async Task<List<ParsedRow<DeviceDto>>> ImportCollectAsync(string content)
        {
            var options = new ImportOptions { ErrorMode = ImportErrorMode.Collect };
            return await TestHelpers.ImportAllAsync<DeviceDto>(Importer, TestHelpers.ToStream(content), options);
        }

        private static byte[] ReadBytes(Stream stream)
        {
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }

        [Fact]
        public async Task GenerateErrorReport_OnlyErrorRows_IncludedInReport()
        {
            var rows = await ImportCollectAsync("设备编码,状态\r\nD001,xyz\r\nD002,online");

            var stream = await Importer.GenerateErrorReportAsync(rows);

            var content = TestHelpers.ReadAll(stream);
            var lines = content.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, lines.Length);
            Assert.Contains("xyz", lines[1]);
            Assert.DoesNotContain("D002", content);
        }

        [Fact]
        public async Task GenerateErrorReport_HeaderRow_IsRowNumberRawColumnsThenError()
        {
            var rows = await ImportCollectAsync("设备编码,状态\r\nD001,xyz");

            var stream = await Importer.GenerateErrorReportAsync(rows);

            var content = TestHelpers.ReadAll(stream);
            var lines = content.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("行号,设备编码,状态,错误信息", lines[0]);
            Assert.StartsWith("2,D001,xyz,", lines[1]);
        }

        [Fact]
        public async Task GenerateErrorReport_ErrorWithQuoteCharacters_Rfc4180Escaped()
        {
            // 错误信息含引号：字段引号包裹并双写内部引号
            var rows = new List<ParsedRow<DeviceDto>>
            {
                new ParsedRow<DeviceDto>
                {
                    RowNumber = 2,
                    Data = new DeviceDto(),
                    RawValues = new Dictionary<string, string> { { "设备编码", "D00\"1" } },
                    Errors = new List<string> { "值 \"D00\"1\" 含引号" }
                }
            };

            var stream = await Importer.GenerateErrorReportAsync(rows);

            var content = TestHelpers.ReadAll(stream);
            var lines = content.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, lines.Length);
            // 回执表头取 RawValues 键首次出现顺序（此行仅"设备编码"一列）
            Assert.Equal("行号,设备编码,错误信息", lines[0]);
            Assert.Equal("2,\"D00\"\"1\",\"值 \"\"D00\"\"1\"\" 含引号\"", lines[1]);
        }

        [Fact]
        public async Task GenerateErrorReport_MultipleErrors_JoinedWithSemicolon()
        {
            var rows = new List<ParsedRow<DeviceDto>>
            {
                new ParsedRow<DeviceDto>
                {
                    RowNumber = 3,
                    Data = new DeviceDto(),
                    RawValues = new Dictionary<string, string>(),
                    Errors = new List<string> { "错误一", "错误二" }
                }
            };

            var stream = await Importer.GenerateErrorReportAsync(rows);

            var content = TestHelpers.ReadAll(stream);
            var lines = content.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.EndsWith("错误一；错误二", lines[1]);
        }

        [Fact]
        public async Task GenerateErrorReport_OutputStartsWithUtf8Bom()
        {
            var rows = await ImportCollectAsync("设备编码,状态\r\nD001,xyz");

            var stream = await Importer.GenerateErrorReportAsync(rows);

            var bytes = ReadBytes(stream);
            Assert.True(bytes.Length >= 3);
            Assert.Equal(0xEF, bytes[0]);
            Assert.Equal(0xBB, bytes[1]);
            Assert.Equal(0xBF, bytes[2]);
        }

        [Fact]
        public async Task GenerateErrorReport_NoErrorRows_HeaderOnly()
        {
            var rows = await ImportCollectAsync("设备编码,状态\r\nD001,online");

            var stream = await Importer.GenerateErrorReportAsync(rows);

            var content = TestHelpers.ReadAll(stream);
            var lines = content.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Single(lines);
            Assert.Equal("行号,错误信息", lines[0]);
        }

        [Fact]
        public async Task GenerateErrorReport_NullRows_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => Importer.GenerateErrorReportAsync<DeviceDto>(null));
        }

        [Fact]
        public async Task GenerateErrorReport_MissingRawKey_OutputsEmptyField()
        {
            // 程序化列名与表头不一致等缺键场景，原始列输出空串不抛异常
            var rows = new List<ParsedRow<DeviceDto>>
            {
                new ParsedRow<DeviceDto>
                {
                    RowNumber = 2,
                    Data = new DeviceDto(),
                    RawValues = new Dictionary<string, string> { { "设备编码", "D001" } },
                    Errors = new List<string> { "错误" }
                }
            };

            var stream = await Importer.GenerateErrorReportAsync(rows);

            var content = TestHelpers.ReadAll(stream);
            var lines = content.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("行号,设备编码,错误信息", lines[0]);
            Assert.Equal("2,D001,错误", lines[1]);
        }
    }
}
