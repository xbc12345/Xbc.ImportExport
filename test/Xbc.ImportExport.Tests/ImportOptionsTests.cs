namespace Xbc.ImportExport.Tests
{
    public class ImportOptionsTests
    {
        [Fact]
        public void Column_DeclaresColumnsAndChains()
        {
            var options = new ImportOptions<DeviceDto>()
                .Column(x => x.Code, "编码")
                .Column(x => x.Name, "名称");

            Assert.Equal(2, options.Columns.Count);
            Assert.Equal("编码", options.Columns[0].Name);
            Assert.Equal(nameof(DeviceDto.Code), options.Columns[0].PropertyName);
            Assert.NotNull(options.Columns[0].Property);
            Assert.Equal("名称", options.Columns[1].Name);
            Assert.Equal(nameof(DeviceDto.Name), options.Columns[1].PropertyName);
        }

        [Fact]
        public void Column_ConfigureAppliesColumnBehavior()
        {
            var options = new ImportOptions<DeviceDto>();

            options.Column(x => x.MadeAt, "生产日期", c => c.Format = "yyyyMMdd");

            Assert.Equal("yyyyMMdd", options.Columns[0].Format);
        }

        [Fact]
        public void Column_NullSelector_ThrowsArgumentNullException()
        {
            var options = new ImportOptions<DeviceDto>();

            Assert.Throws<ArgumentNullException>(() => options.Column<string>(null, "编码"));
        }

        [Fact]
        public void Column_NameOmitted_UsesPropertyName()
        {
            var options = new ImportOptions<DeviceDto>().Column(x => x.Code);

            var column = Assert.Single(options.Columns);
            Assert.Equal(nameof(DeviceDto.Code), column.Name);
            Assert.Equal(nameof(DeviceDto.Code), column.PropertyName);
        }

        [Fact]
        public void Column_NonMemberExpression_ThrowsArgumentException()
        {
            var options = new ImportOptions<DeviceDto>();

            Assert.Throws<ArgumentException>(() => options.Column(x => x.ToString(), "字符串"));
        }

        [Fact]
        public void Column_NestedMemberExpression_ThrowsArgumentException()
        {
            var options = new ImportOptions<DeviceDto>();

            Assert.Throws<ArgumentException>(() => options.Column(x => x.Code.Length, "长度"));
        }

        [Fact]
        public void Column_NegatedValueExpression_ThrowsArgumentException()
        {
            // 负号不属于类型转换节点，静默解包会丢弃负号，须按非属性访问拒绝
            var options = new ImportOptions<DeviceDto>();

            Assert.Throws<ArgumentException>(() => options.Column(x => -x.Count, "负数"));
        }

        [Fact]
        public void Column_StaticMemberExpression_ThrowsArgumentException()
        {
            var options = new ImportOptions<DeviceDto>();

            Assert.Throws<ArgumentException>(() => options.Column(x => DateTime.Now, "时间"));
        }

        [Fact]
        public void Column_ValueTypeProperty_UnwrapsConvertNode()
        {
            var options = new ImportOptions<DeviceDto>();

            options.Column<object>(x => x.Status, "状态");

            Assert.Equal("状态", options.Columns[0].Name);
            Assert.Equal(nameof(DeviceDto.Status), options.Columns[0].PropertyName);
        }

        [Fact]
        public void Column_LambdaBodyExpression_ThrowsArgumentException()
        {
            // TValue 显式声明为委托类型时，body 是嵌套 lambda 而非属性访问，应拒绝
            var options = new ImportOptions<DeviceDto>();

            Assert.Throws<ArgumentException>(() => options.Column<Func<string, string>>(x => s => s + x.Code, "映射"));
        }

        [Fact]
        public async Task Column_DuplicateProperty_ThrowsImportException()
        {
            // 特性路径由 AllowMultiple = false 防重，程序化路径由解析器查重防线拦截
            var options = new ImportOptions<DeviceDto>()
                .Column(x => x.Code, "编码")
                .Column(x => x.Code, "设备编码");
            var importer = new Xbc.ImportExport.Csv.CsvImporter();
            var stream = TestHelpers.ToStream("编码,名称\r\nD001,传感器\r\n");

            var exception = await Assert.ThrowsAsync<ImportException>(
                () => TestHelpers.ImportAllAsync<DeviceDto>(importer, stream, options));
            Assert.Contains("重复绑定属性 'Code'", exception.Message);
        }

        [Fact]
        public async Task Column_DuplicateName_ThrowsImportException()
        {
            var options = new ImportOptions<DeviceDto>()
                .Column(x => x.Code, "编码")
                .Column(x => x.Name, "编码");
            var importer = new Xbc.ImportExport.Csv.CsvImporter();
            var stream = TestHelpers.ToStream("编码,名称\r\nD001,传感器\r\n");

            var exception = await Assert.ThrowsAsync<ImportException>(
                () => TestHelpers.ImportAllAsync<DeviceDto>(importer, stream, options));
            Assert.Contains("程序化导入列名 '编码' 重复", exception.Message);
        }
    }
}
