namespace Xbc.ImportExport.Tests
{
    public class ImportColumnResolverTests
    {
        [Fact]
        public void ResolveImportColumns_NoAttributes_ReturnsEmptyList()
        {
            var columns = ImportColumnResolver.ResolveImportColumns(typeof(PlainDto));

            Assert.Empty(columns);
        }

        [Fact]
        public void ResolveImportColumns_OrdersByOrderThenDeclaration()
        {
            var columns = ImportColumnResolver.ResolveImportColumns(typeof(MixedOrderDto));

            var names = columns.Select(c => c.Name).ToArray();
            Assert.Equal(new[] { "第一", "第二", "无序A", "无序B" }, names);
        }

        [Fact]
        public void ResolveImportColumns_DeclarationOrder_WhenNoOrderSpecified()
        {
            var columns = ImportColumnResolver.ResolveImportColumns(typeof(OrderlessDto));

            var names = columns.Select(c => c.Name).ToArray();
            Assert.Equal(new[] { "甲", "乙", "丙" }, names);
        }

        [Fact]
        public void ResolveImportColumns_MapsAttributeProperties()
        {
            var columns = ImportColumnResolver.ResolveImportColumns(typeof(DeviceDto));
            var code = columns.First(c => c.Property.Name == nameof(DeviceDto.Code));

            Assert.Equal("设备编码", code.Name);
        }

        [Fact]
        public void ResolveTemplateColumns_MapsAttributeProperties()
        {
            var columns = ImportColumnResolver.ResolveTemplateColumns(typeof(DeviceDto));
            var code = columns.First(c => c.PropertyName == nameof(DeviceDto.Code));

            Assert.Equal("设备编码", code.Name);
            Assert.False(code.IsRequired);
            Assert.Equal(30, code.Width);
        }

        [Fact]
        public void ResolveImportColumns_NameDefaultsToPropertyName()
        {
            var columns = ImportColumnResolver.ResolveImportColumns(typeof(UnnamedDto));

            Assert.All(columns, c => Assert.Equal(c.Property.Name, c.Name));
        }

        [Fact]
        public void ResolveImportColumns_CarriesFormatAndDefaultValue()
        {
            var columns = ImportColumnResolver.ResolveImportColumns(typeof(FormattedDto));
            var day = columns.First(c => c.Property.Name == nameof(FormattedDto.Day));

            Assert.Equal("yyyyMMdd", day.Format);
            Assert.Null(day.DefaultValue);
        }

        [Fact]
        public void ResolveTemplateColumns_CarriesLayoutAndDisplayFields()
        {
            var columns = ImportColumnResolver.ResolveTemplateColumns(typeof(FormattedDto));
            var day = columns.First(c => c.PropertyName == nameof(FormattedDto.Day));

            Assert.Equal("yyyyMMdd", day.Format);
            Assert.Equal(15, day.Width);
            Assert.Equal(string.Empty, day.Comment);
        }

        [Fact]
        public void ResolveTemplateColumns_EnumFillsAllowedValues()
        {
            var columns = ImportColumnResolver.ResolveTemplateColumns(typeof(DeviceDto));
            var status = columns.First(c => c.PropertyName == nameof(DeviceDto.Status));

            Assert.Equal(TemplateColumnType.Enum, status.DataType);
            Assert.Equal(new[] { "Online", "Offline", "Unknown" }, status.AllowedValues);
        }

        [Fact]
        public void ResolveImportColumns_NonLiteralTypeWithoutConverter_Throws()
        {
            var ex = Assert.Throws<ImportException>(() => ImportColumnResolver.ResolveImportColumns(typeof(NonLiteralImportDto)));

            Assert.Contains("字面量类型", ex.Message);
        }

        [Fact]
        public void ResolveImportColumns_NonLiteralTypeWithConverter_Passes()
        {
            var columns = ImportColumnResolver.ResolveImportColumns(typeof(StringListDto));

            var column = Assert.Single(columns);
            Assert.NotNull(column.Converter);
        }

        [Fact]
        public void ResolveTemplateColumns_ResolvesAllDataTypes()
        {
            var columns = ImportColumnResolver.ResolveTemplateColumns(typeof(TypesDto));
            var byProperty = columns.ToDictionary(c => c.PropertyName, c => c.DataType);

            Assert.Equal(TemplateColumnType.String, byProperty[nameof(TypesDto.S)]);
            Assert.Equal(TemplateColumnType.Integer, byProperty[nameof(TypesDto.SH)]);
            Assert.Equal(TemplateColumnType.Integer, byProperty[nameof(TypesDto.BY)]);
            Assert.Equal(TemplateColumnType.Integer, byProperty[nameof(TypesDto.SB)]);
            Assert.Equal(TemplateColumnType.Integer, byProperty[nameof(TypesDto.US)]);
            Assert.Equal(TemplateColumnType.Integer, byProperty[nameof(TypesDto.I)]);
            Assert.Equal(TemplateColumnType.Integer, byProperty[nameof(TypesDto.UI)]);
            Assert.Equal(TemplateColumnType.Long, byProperty[nameof(TypesDto.L)]);
            Assert.Equal(TemplateColumnType.Long, byProperty[nameof(TypesDto.UL)]);
            Assert.Equal(TemplateColumnType.Number, byProperty[nameof(TypesDto.F)]);
            Assert.Equal(TemplateColumnType.Number, byProperty[nameof(TypesDto.M)]);
            Assert.Equal(TemplateColumnType.Number, byProperty[nameof(TypesDto.D)]);
            Assert.Equal(TemplateColumnType.Boolean, byProperty[nameof(TypesDto.B)]);
            Assert.Equal(TemplateColumnType.DateTime, byProperty[nameof(TypesDto.DT)]);
            Assert.Equal(TemplateColumnType.DateTime, byProperty[nameof(TypesDto.DTO)]);
            Assert.Equal(TemplateColumnType.String, byProperty[nameof(TypesDto.TS)]);
            Assert.Equal(TemplateColumnType.Guid, byProperty[nameof(TypesDto.G)]);
            Assert.Equal(TemplateColumnType.Enum, byProperty[nameof(TypesDto.E)]);
            Assert.Equal(TemplateColumnType.Integer, byProperty[nameof(TypesDto.N)]);
        }

        [Fact]
        public void ResolveImportColumns_ConverterType_WrapsConverterDelegate()
        {
            var columns = ImportColumnResolver.ResolveImportColumns(typeof(ImportConverterDto));

            var status = columns.Find(c => c.Property.Name == "Status");
            Assert.NotNull(status.Converter);
            Assert.Equal(1, status.Converter("在线"));
            Assert.Equal(2, status.Converter("离线"));
            Assert.Equal(9, status.Converter("其他"));

            var code = columns.Find(c => c.Property.Name == "Code");
            Assert.Null(code.Converter);
        }

        [Fact]
        public void ResolveImportColumns_ConverterTypeNotImplementingInterface_ThrowsImportException()
        {
            var ex = Assert.Throws<ImportException>(() => ImportColumnResolver.ResolveImportColumns(typeof(InvalidImportConverterDto)));

            Assert.Contains("未实现", ex.Message);
            Assert.Contains("IImportValueConverter", ex.Message);
        }

        [Fact]
        public void ResolveImportColumns_ConverterTypeWithoutParameterlessCtor_ThrowsImportException()
        {
            var ex = Assert.Throws<ImportException>(() => ImportColumnResolver.ResolveImportColumns(typeof(NoCtorImportConverterDto)));

            Assert.Contains("未定义无参构造函数", ex.Message);
        }

        [Fact]
        public void Resolve_CachesResultPerType()
        {
            var first = ImportColumnResolver.ResolveImportColumns(typeof(DeviceDto));
            var second = ImportColumnResolver.ResolveImportColumns(typeof(DeviceDto));

            Assert.Same(first, second);
        }

        [Fact]
        public void ResolveImportColumns_PropertyWithoutSetter_ThrowsImportException()
        {
            var ex = Assert.Throws<ImportException>(() => ImportColumnResolver.ResolveImportColumns(typeof(ReadOnlyPropDto)));

            Assert.Contains("缺少 setter", ex.Message);
            Assert.Contains("Code", ex.Message);
        }

        [Fact]
        public void ResolveImportColumns_DuplicateColumnNames_ThrowsImportException()
        {
            // 特性路径列名唯一性校验（对齐程序化路径）：两个属性声明同名列名快速失败
            var ex = Assert.Throws<ImportException>(() => ImportColumnResolver.ResolveImportColumns(typeof(DuplicateImportNameDto)));

            Assert.Contains("列名 '编码' 重复", ex.Message);
            Assert.Contains("Code", ex.Message);
            Assert.Contains("Name", ex.Message);
        }
    }
}
