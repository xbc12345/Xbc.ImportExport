namespace Xbc.ImportExport.Tests
{
    public class ExportColumnResolverTests
    {
        [Fact]
        public void ResolveExportColumns_NoAttributes_ReturnsEmptyList()
        {
            var columns = ExportColumnResolver.ResolveExportColumns(typeof(PlainDto));

            Assert.Empty(columns);
        }

        [Fact]
        public void ResolveExportColumns_NonLiteralTypeWithoutConverter_Throws()
        {
            var ex = Assert.Throws<ExportException>(() => ExportColumnResolver.ResolveExportColumns(typeof(NonLiteralExportDto)));

            Assert.Contains("字面量类型", ex.Message);
        }

        [Fact]
        public void ResolveExportColumns_NonLiteralTypeWithConverter_Passes()
        {
            var columns = ExportColumnResolver.ResolveExportColumns(typeof(StringListDto));

            var column = Assert.Single(columns);
            Assert.NotNull(column.Converter);
        }

        [Fact]
        public void ResolveExportColumns_OrdersByOrderThenDeclaration()
        {
            var columns = ExportColumnResolver.ResolveExportColumns(typeof(MixedOrderDto));

            var names = columns.Select(c => c.Name).ToArray();
            Assert.Equal(new[] { "第一", "第二", "无序A", "无序B" }, names);
        }

        [Fact]
        public void ResolveExportColumns_MapsProperties()
        {
            var columns = ExportColumnResolver.ResolveExportColumns(typeof(DeviceDto));
            var code = columns.First(c => c.PropertyName == nameof(DeviceDto.Code));

            Assert.Equal("设备编码", code.Name);
            Assert.Equal(1, code.Order);
            Assert.Null(code.Converter);
        }

        [Fact]
        public void ResolveExportColumns_NameDefaultsToPropertyName()
        {
            var columns = ExportColumnResolver.ResolveExportColumns(typeof(UnnamedDto));

            Assert.All(columns, c => Assert.Equal(c.PropertyName, c.Name));
        }

        [Fact]
        public void ResolveExportColumns_ConverterType_WrapsConverterDelegate()
        {
            var columns = ExportColumnResolver.ResolveExportColumns(typeof(ConverterDto));

            var status = columns.Find(c => c.PropertyName == "Status");
            Assert.NotNull(status.Converter);
            Assert.Equal("在线", status.Converter(1));
            Assert.Equal("离线", status.Converter(2));
            Assert.Equal("未知", status.Converter(9));

            var code = columns.Find(c => c.PropertyName == "Code");
            Assert.Null(code.Converter);
        }

        [Fact]
        public void ResolveExportColumns_ConverterTypeNotImplementingInterface_ThrowsExportException()
        {
            var ex = Assert.Throws<ExportException>(() => ExportColumnResolver.ResolveExportColumns(typeof(InvalidConverterDto)));

            Assert.Contains("未实现", ex.Message);
            Assert.Contains("IExportValueConverter", ex.Message);
        }

        [Fact]
        public void ResolveExportColumns_ConverterTypeWithoutParameterlessCtor_ThrowsExportException()
        {
            var ex = Assert.Throws<ExportException>(() => ExportColumnResolver.ResolveExportColumns(typeof(NoCtorConverterDto)));

            Assert.Contains("未定义无参构造函数", ex.Message);
        }

        [Fact]
        public void ResolveExportColumns_PropertyWithoutGetter_ThrowsExportException()
        {
            var ex = Assert.Throws<ExportException>(() => ExportColumnResolver.ResolveExportColumns(typeof(WriteOnlyPropDto)));

            Assert.Contains("缺少 getter", ex.Message);
            Assert.Contains("Code", ex.Message);
        }

        [Fact]
        public void ResolveExportColumns_GetOnlyProperty_ResolvesColumn()
        {
            var columns = ExportColumnResolver.ResolveExportColumns(typeof(GetOnlyExportDto));

            var column = Assert.Single(columns);
            Assert.Equal("计算值", column.Name);
        }

        [Fact]
        public void ResolveExportColumns_DuplicateColumnNames_ThrowsExportException()
        {
            // 特性路径列名唯一性校验（对齐程序化路径）：两个属性声明同名列名快速失败
            var ex = Assert.Throws<ExportException>(() => ExportColumnResolver.ResolveExportColumns(typeof(DuplicateExportNameDto)));

            Assert.Contains("列名 '编码' 重复", ex.Message);
            Assert.Contains("Code", ex.Message);
            Assert.Contains("Name", ex.Message);
        }
    }
}
