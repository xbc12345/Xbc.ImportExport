namespace Xbc.ImportExport.Tests
{
    public class ExportOptionsTests
    {
        [Fact]
        public void Column_WritesColumnAndChains()
        {
            var options = new ExportOptions<DeviceDto>()
                .Column(x => x.Code, "编码")
                .Column(x => x.Name, "名称");

            Assert.Equal(2, options.Columns.Count);
            Assert.Equal(nameof(DeviceDto.Code), options.Columns[0].PropertyName);
            Assert.NotNull(options.Columns[0].Property);
            Assert.Equal("编码", options.Columns[0].Name);
            Assert.Equal(nameof(DeviceDto.Name), options.Columns[1].PropertyName);
            Assert.NotNull(options.Columns[1].Property);
            Assert.Equal("名称", options.Columns[1].Name);
        }

        [Fact]
        public void Column_NameOmitted_UsesPropertyName()
        {
            var options = new ExportOptions<DeviceDto>().Column(x => x.Code);

            var column = Assert.Single(options.Columns);
            Assert.Equal(nameof(DeviceDto.Code), column.Name);
        }

        [Fact]
        public void Column_NullSelector_ThrowsArgumentNullException()
        {
            var options = new ExportOptions<DeviceDto>();

            Assert.Throws<ArgumentNullException>(() => options.Column<string>(null, "编码"));
        }

        [Fact]
        public void Column_NestedMemberExpression_ThrowsArgumentException()
        {
            var options = new ExportOptions<DeviceDto>();

            Assert.Throws<ArgumentException>(() => options.Column(x => x.Code.Length, "长度"));
        }

        [Fact]
        public void Column_NegatedValueExpression_ThrowsArgumentException()
        {
            // 负号不属于类型转换节点，静默解包会丢弃负号，须按非属性访问拒绝
            var options = new ExportOptions<DeviceDto>();

            Assert.Throws<ArgumentException>(() => options.Column(x => -x.Count, "负数"));
        }

        [Fact]
        public void Column_ConfigureAppliesMetadata()
        {
            var options = new ExportOptions<DeviceDto>()
                .Column(x => x.MadeAt, "生产日期", c =>
                {
                    c.Format = "yyyy-MM-dd";
                    c.Width = 30;
                    c.Converter = v => v;
                });

            var column = Assert.Single(options.Columns);
            Assert.Equal("yyyy-MM-dd", column.Format);
            Assert.Equal(30, column.Width);
            Assert.NotNull(column.Converter);
        }

        [Fact]
        public async Task Column_CsvExportEndToEnd_WritesDeclaredColumns()
        {
            var exporter = new Xbc.ImportExport.Csv.CsvExporter();
            var options = new ExportOptions<DeviceDto>()
                .Column(x => x.Code, "编码")
                .Column(x => x.Name, "名称");
            var item = new DeviceDto { Code = "D001", Name = "传感器" };

            var stream = await exporter.ExportAsync(TestHelpers.DataOf(item), options);
            var content = TestHelpers.ReadAll(stream);

            Assert.Equal("编码,名称\r\nD001,传感器\r\n", content);
        }

        [Fact]
        public async Task Column_DuplicateProperty_ThrowsExportException()
        {
            // 特性路径由 AllowMultiple = false 防重，程序化路径由解析器查重防线拦截
            var exporter = new Xbc.ImportExport.Csv.CsvExporter();
            var options = new ExportOptions<DeviceDto>()
                .Column(x => x.Code, "编码")
                .Column(x => x.Code, "设备编码");

            var exception = await Assert.ThrowsAsync<ExportException>(
                () => exporter.ExportAsync(TestHelpers.DataOf(new DeviceDto { Code = "D001" }), options));
            Assert.Contains("重复绑定属性 'Code'", exception.Message);
        }

        [Fact]
        public async Task Column_DuplicateName_ThrowsExportException()
        {
            var exporter = new Xbc.ImportExport.Csv.CsvExporter();
            var options = new ExportOptions<DeviceDto>()
                .Column(x => x.Code, "编码")
                .Column(x => x.Name, "编码");

            var exception = await Assert.ThrowsAsync<ExportException>(
                () => exporter.ExportAsync(TestHelpers.DataOf(new DeviceDto { Code = "D001" }), options));
            Assert.Contains("程序化导出列名 '编码' 重复", exception.Message);
        }
    }
}
