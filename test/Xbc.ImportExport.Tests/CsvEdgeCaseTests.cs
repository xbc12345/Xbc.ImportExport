using Xbc.ImportExport.Csv;

namespace Xbc.ImportExport.Tests
{
    /// <summary>
    /// CSV 边界行为：表头裁剪与大小写匹配、引号字段完整性、公式防护前缀全覆盖、
    /// 错误回执公式防护、DateTimeOffset/TimeSpan 导出文本、Columns = null 回退特性列。
    /// </summary>
    public class CsvEdgeCaseTests
    {
        private readonly CsvImporter importer = new CsvImporter();
        private readonly CsvExporter exporter = new CsvExporter();

        [Fact]
        public async Task Import_HeaderWithSurroundingSpaces_TrimmedForMatch()
        {
            // 表头单元格前后空白裁剪后匹配列名，数据绑定仍取原列索引
            var content = " 设备编码 , 设备名称 \r\nD001,温度传感器";

            var rows = await TestHelpers.ImportAllAsync<SimpleDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
        }

        [Fact]
        public async Task Import_HeaderCaseInsensitive_Matches()
        {
            // 表头匹配大小写不敏感（OrdinalIgnoreCase）：全大写表头匹配 "Alpha" 列名
            var content = "ALPHA,BETA\r\nva,vb";

            var rows = await TestHelpers.ImportAllAsync<UnnamedDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal("va", row.Data.Alpha);
            Assert.Equal("vb", row.Data.Beta);
        }

        [Fact]
        public async Task Import_QuotedFieldWithCommaAndNewline_PreservedIntact()
        {
            // RFC 4180 引号字段：内嵌逗号与换行不拆列不断行，RawValues 同步保真
            var content = "设备编码,设备名称\r\n\"D,001\",\"温度\r\n传感器\"";

            var rows = await TestHelpers.ImportAllAsync<SimpleDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal("D,001", row.Data.Code);
            Assert.Equal("温度\r\n传感器", row.Data.Name);
            Assert.Equal("D,001", row.RawValues["设备编码"]);
        }

        [Fact]
        public async Task Export_TabAndCarriageReturnPrefixes_EscapedLikeOtherFormulaTriggers()
        {
            // 公式触发字符全覆盖：制表符与回车前缀同样加 ' 防护（=、+、-、@ 已有覆盖）
            var options = new ExportOptions
            {
                Columns = { new ExportColumn { PropertyName = nameof(PlainDto.Code), Name = "文本" } }
            };
            var stream = await exporter.ExportAsync(
                TestHelpers.DataOf(new PlainDto { Code = "\tSUM(A1)" }),
                options);

            var lines = TestHelpers.ReadAll(stream).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            Assert.Equal("文本", lines[0]);
            Assert.Equal("'\tSUM(A1)", lines[1]);
        }

        [Fact]
        public async Task ErrorReport_FormulaTriggerRawValue_EscapedWithQuotePrefix()
        {
            // 错误回执的原始列同样经公式防护，回执回传后重新打开不解释为公式
            var rows = new[]
            {
                new ParsedRow<SimpleDto>
                {
                    RowNumber = 2,
                    Data = new SimpleDto { Code = "-1", Name = "传感器" },
                    RawValues = new Dictionary<string, string> { ["设备编码"] = "-1" },
                    Errors = new List<string> { "编码不存在" }
                }
            };

            var stream = await importer.GenerateErrorReportAsync(rows);

            var content = TestHelpers.ReadAll(stream);
            // 表头之后为 行号,设备编码,错误信息；原始值 "-1" 加 ' 前缀防公式解释
            Assert.Contains("'-1", content);
        }

        [Fact]
        public async Task Export_DateTimeOffsetAndTimeSpan_WritesInvariantText()
        {
            // DateTimeOffset/TimeSpan 经 InvariantCulture 文本化（CSV 无类型单元格，数值/文本恒定格式）
            var item = new ExtendedTypesDto
            {
                TS = new TimeSpan(1, 2, 3),
                DTO = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(8))
            };

            var stream = await exporter.ExportAsync(TestHelpers.DataOf(item), new ExportOptions());

            var content = TestHelpers.ReadAll(stream);
            var line = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)[1];
            Assert.Contains("01:02:03", line);
            Assert.Contains("2026", line);
        }

        [Fact]
        public async Task Export_NullColumnsList_FallsBackToAttributes()
        {
            // Columns 显式置 null（而非空 List）同样回退特性列解析
            var options = new ExportOptions { Columns = null };

            var stream = await exporter.ExportAsync(
                TestHelpers.DataOf(new SimpleExportDto { Code = "D001" }),
                options);

            var content = TestHelpers.ReadAll(stream);
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("编码", lines[0]);
            Assert.Equal("D001", lines[1]);
        }

        [Fact]
        public async Task Export_ConverterReturnsNull_WritesEmptyField()
        {
            // 转换器返回 null 落为空单元格（与 null 属性值一致），不写空串噪声
            var stream = await exporter.ExportAsync(
                TestHelpers.DataOf(new NullConverterExportDto { Code = "D001" }),
                new ExportOptions());

            var content = TestHelpers.ReadAll(stream);
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            Assert.Equal("编码", lines[0]);
            Assert.Equal(string.Empty, lines[1]);
        }

        /// <summary>最小导出实体（回退特性列验证用）。</summary>
        public class SimpleExportDto
        {
            [ExportColumn("编码")]
            public string Code { get; set; }
        }
    }
}
