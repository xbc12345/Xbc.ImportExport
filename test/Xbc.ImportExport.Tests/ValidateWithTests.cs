using Xbc.ImportExport.Csv;

namespace Xbc.ImportExport.Tests
{
    public class ValidateWithTests
    {
        private readonly CsvImporter importer = new CsvImporter();

        [Fact]
        public async Task ValidateWith_BusinessError_MergesIntoRowErrors()
        {
            var content = "设备编码,设备名称\r\nD001,温度传感器";

            var rows = new List<ParsedRow<SimpleDto>>();
            await foreach (var row in importer.ImportAsync<SimpleDto>(TestHelpers.ToStream(content), new ImportOptions()).ValidateWith(
                d => $"编码 {d.Code} 不存在"))
            {
                rows.Add(row);
            }

            var result = Assert.Single(rows);
            Assert.Equal("D001", result.Data.Code);
            var error = Assert.Single(result.Errors);
            Assert.Equal("编码 D001 不存在", error);
        }

        [Fact]
        public async Task ValidateWith_MultipleValidators_ChainErrorsInOrder()
        {
            // 多条业务规则链式多个 ValidateWith，错误按校验器顺序合并
            var content = "设备编码,设备名称\r\nD001,温度传感器";

            var rows = new List<ParsedRow<SimpleDto>>();
            await foreach (var row in importer.ImportAsync<SimpleDto>(TestHelpers.ToStream(content), new ImportOptions())
                .ValidateWith(d => "错误一")
                .ValidateWith(d => "错误二"))
            {
                rows.Add(row);
            }

            var result = Assert.Single(rows);
            Assert.Equal(2, result.Errors.Count);
            Assert.Equal("错误一", result.Errors[0]);
            Assert.Equal("错误二", result.Errors[1]);
        }

        [Fact]
        public async Task ValidateWith_CleanRow_PassesThroughUnchanged()
        {
            var content = "设备编码,设备名称\r\nD001,温度传感器";

            var rows = new List<ParsedRow<SimpleDto>>();
            await foreach (var row in importer.ImportAsync<SimpleDto>(TestHelpers.ToStream(content), new ImportOptions()).ValidateWith(
                d => null))
            {
                rows.Add(row);
            }

            var result = Assert.Single(rows);
            Assert.Empty(result.Errors);
            Assert.Equal("D001", result.Data.Code);
        }

        [Fact]
        public async Task ValidateWith_CollectMode_ParseAndBusinessErrorsCoexist()
        {
            // Collect 模式下解析错误（状态无法解析）与业务校验错误同处一行 Errors
            var content = "设备编码,状态\r\nD001,xyz";
            var options = new ImportOptions { ErrorMode = ImportErrorMode.Collect };

            var rows = new List<ParsedRow<DeviceDto>>();
            await foreach (var row in importer.ImportAsync<DeviceDto>(TestHelpers.ToStream(content), options).ValidateWith(
                d => "业务错误"))
            {
                rows.Add(row);
            }

            var result = Assert.Single(rows);
            Assert.Equal(2, result.Errors.Count);
            Assert.Contains("状态", result.Errors[0]);
            Assert.Equal("业务错误", result.Errors[1]);
        }

        [Fact]
        public async Task ValidateWith_NullValidator_ThrowsArgumentNullException()
        {
            var content = "设备编码,设备名称\r\nD001,温度传感器";

            // 装饰器对 null 校验委托立即抛出（枚举触发）
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await foreach (var row in importer.ImportAsync<SimpleDto>(TestHelpers.ToStream(content), new ImportOptions()).ValidateWith<SimpleDto>(null))
                {
                }
            });
        }

        [Fact]
        public async Task ValidateWith_NullDataRow_PassesThroughWithoutValidation()
        {
            // Data 为 null 的行（如解析失败产物）原样透传，不参与业务校验
            ParsedRow<SimpleDto>[] source =
            {
                new ParsedRow<SimpleDto> { RowNumber = 2, Data = null, Errors = new List<string>() }
            };
            var validated = new List<string>();
            var validatedNullData = false;

            await foreach (var row in TestHelpers.DataOf(source[0]).ValidateWith(
                d =>
                {
                    validated.Add(d.Code);
                    return "不应到达";
                }))
            {
                if (row.Data == null)
                    validatedNullData = true;
            }

            Assert.True(validatedNullData);
            Assert.Empty(validated);
        }
    }
}
