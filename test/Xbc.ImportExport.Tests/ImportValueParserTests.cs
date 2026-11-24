namespace Xbc.ImportExport.Tests
{
    /// <summary>导入值解析器测试（直测解析边界：decimal/float 独立解析与转换器列默认值回退）。</summary>
    public class ImportValueParserTests
    {
        private static ImportColumnMapping MappingOf(Type type, string propertyName, Action<ImportColumn> configure = null)
        {
            var column = new ImportColumn
            {
                Name = "测试列",
                PropertyName = propertyName,
                Property = type.GetProperty(propertyName)
            };

            configure?.Invoke(column);
            return new ImportColumnMapping { Column = column };
        }

        [Fact]
        public void Resolve_DecimalValue_PreservesFullPrecision()
        {
            // decimal 直接解析不经 double 中转（double 仅 15-17 位精度，会截断 28 位 decimal）
            var mapping = MappingOf(typeof(TypesDto), nameof(TypesDto.M));

            var result = ImportValueParser.Resolve(
                mapping, "0.12345678901234567890123456789", ImportErrorMode.Strict, 1, new List<string>());

            Assert.Equal(0.12345678901234567890123456789m, result);
        }

        [Fact]
        public void Resolve_NullableTypeFailure_ShowsUnderlyingTypeName()
        {
            // 可空类型解析失败消息显示底层类型名（Int32）而非 Nullable`1
            var mapping = MappingOf(typeof(DeviceDto), nameof(DeviceDto.Remark));

            var exception = Assert.Throws<ImportDataException>(() => ImportValueParser.Resolve(
                mapping, "abc", ImportErrorMode.Strict, 1, new List<string>()));

            Assert.Contains("Int32", exception.Message);
        }

        [Fact]
        public void Resolve_FloatMaxValue_ParsesExactly()
        {
            // float 独立解析不经 double 中转，MaxValue 往返字符串精确还原
            var mapping = MappingOf(typeof(TypesDto), nameof(TypesDto.F));

            var result = ImportValueParser.Resolve(
                mapping, "3.40282347E+38", ImportErrorMode.Strict, 1, new List<string>());

            Assert.Equal(float.MaxValue, result);
        }

        [Fact]
        public void Resolve_FloatOverflow_ParsesAsInfinity()
        {
            // 超出 float 范围按 IEEE 754 解析为无穷（独立分支不经 double 中转，不抛 OverflowException）
            var mapping = MappingOf(typeof(TypesDto), nameof(TypesDto.F));

            var result = ImportValueParser.Resolve(
                mapping, "3.5E+38", ImportErrorMode.Strict, 1, new List<string>());

            Assert.Equal(float.PositiveInfinity, result);
        }

        [Fact]
        public void Resolve_ConverterColumnStringDefaultValue_FallsBackToTypeDefault()
        {
            // 转换器列声明字符串默认值（与集合属性类型不兼容），解析失败静默回退类型默认值
            var mapping = MappingOf(typeof(UnmappedFieldsDto), nameof(UnmappedFieldsDto.Tags), c =>
            {
                c.Converter = v => new List<string>(v.Split(','));
                c.DefaultValue = "A,B";
            });

            var result = ImportValueParser.Resolve(
                mapping, string.Empty, ImportErrorMode.Strict, 1, new List<string>());

            Assert.Null(result);
        }

        [Fact]
        public void Resolve_FlagsEnum_CombinedNames_Parse()
        {
            // [Flags] 枚举按标准解析语义放行组合值（名称有效性由 Enum.Parse 校验）
            var mapping = MappingOf(typeof(FlagsDto), nameof(FlagsDto.Rights));

            var result = ImportValueParser.Resolve(
                mapping, "Read, Write", ImportErrorMode.Strict, 1, new List<string>());

            Assert.Equal(Permissions.Read | Permissions.Write, result);
        }

        [Fact]
        public void Resolve_PlainEnum_UndefinedNumericValue_Rejected()
        {
            // 非 [Flags] 枚举越界数值仍被拦截（IsDefined 防线不因 Flags 放行而弱化）
            var mapping = MappingOf(typeof(DeviceDto), nameof(DeviceDto.Status));

            Assert.Throws<ImportDataException>(() => ImportValueParser.Resolve(
                mapping, "99", ImportErrorMode.Strict, 1, new List<string>()));
        }
    }
}
