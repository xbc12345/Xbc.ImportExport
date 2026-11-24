namespace Xbc.ImportExport.Tests
{
    /// <summary>全字段类型测试实体（导入/导出共用，覆盖全部类型转换分支）。</summary>
    public class DeviceDto
    {
        [ImportColumn("设备编码", Order = 1, Width = 30)]
        [ExportColumn("设备编码", Order = 1)]
        public string Code { get; set; }

        [ImportColumn("设备名称", Order = 2, Description = "设备显示名称")]
        [ExportColumn("设备名称", Order = 2)]
        public string Name { get; set; }

        [ImportColumn("启用", Order = 3, DefaultValue = true)]
        [ExportColumn("启用", Order = 3)]
        public bool Enabled { get; set; }

        [ImportColumn("数量", Order = 4)]
        [ExportColumn("数量", Order = 4)]
        public int Count { get; set; }

        [ImportColumn("价格", Order = 5)]
        [ExportColumn("价格", Order = 5)]
        public double Price { get; set; }

        [ImportColumn("生产日期", Order = 6)]
        [ExportColumn("生产日期", Order = 6, Format = "yyyy-MM-dd")]
        public DateTime MadeAt { get; set; }

        [ImportColumn("编号", Order = 7)]
        [ExportColumn("编号", Order = 7)]
        public Guid Uid { get; set; }

        [ImportColumn("状态", Order = 8)]
        [ExportColumn("状态", Order = 8)]
        public DeviceStatus Status { get; set; }

        [ImportColumn("备注数", Order = 9)]
        [ExportColumn("备注数", Order = 9)]
        public int? Remark { get; set; }
    }

    public enum DeviceStatus
    {
        Online,
        Offline,
        Unknown
    }

    /// <summary>无 Order 声明实体（验证声明顺序回退排序）。</summary>
    public class OrderlessDto
    {
        [ImportColumn("甲")]
        [ExportColumn("甲")]
        public string A { get; set; }

        [ImportColumn("乙")]
        [ExportColumn("乙")]
        public string B { get; set; }

        [ImportColumn("丙")]
        [ExportColumn("丙")]
        public string C { get; set; }
    }

    /// <summary>省略列名特性实体（验证 Name 回退属性名）。</summary>
    public class UnnamedDto
    {
        [ImportColumn]
        [ExportColumn]
        public string Alpha { get; set; }

        [ImportColumn]
        [ExportColumn]
        public string Beta { get; set; }
    }

    /// <summary>混合 Order 声明实体（验证 Order 优先、0 值排后的排序规则）。</summary>
    public class MixedOrderDto
    {
        [ImportColumn("无序A")]
        [ExportColumn("无序A")]
        public string A { get; set; }

        [ImportColumn("第二", Order = 2)]
        [ExportColumn("第二", Order = 2)]
        public string B { get; set; }

        [ImportColumn("无序B")]
        [ExportColumn("无序B")]
        public string C { get; set; }

        [ImportColumn("第一", Order = 1)]
        [ExportColumn("第一", Order = 1)]
        public string D { get; set; }
    }

    /// <summary>无任何列特性实体（用于空列与自定义映射场景）。</summary>
    public class PlainDto
    {
        public string Code { get; set; }

        public string Name { get; set; }
    }

    /// <summary>含读写索引器的实体（验证索引器不参与导入绑定）。</summary>
    public class IndexedDto
    {
        public string Code { get; set; }

        public string this[int index]
        {
            get { return index.ToString(); }
            set { }
        }
    }

    /// <summary>类型推断测试实体（覆盖 ResolveColumnType 全部分支）。</summary>
    public class TypesDto
    {
        [ImportColumn("S")] public string S { get; set; }
        [ImportColumn("SH")] public short SH { get; set; }
        [ImportColumn("BY")] public byte BY { get; set; }
        [ImportColumn("SB")] public sbyte SB { get; set; }
        [ImportColumn("US")] public ushort US { get; set; }
        [ImportColumn("I")] public int I { get; set; }
        [ImportColumn("UI")] public uint UI { get; set; }
        [ImportColumn("L")] public long L { get; set; }
        [ImportColumn("UL")] public ulong UL { get; set; }
        [ImportColumn("F")] public float F { get; set; }
        [ImportColumn("M")] public decimal M { get; set; }
        [ImportColumn("D")] public double D { get; set; }
        [ImportColumn("B")] public bool B { get; set; }
        [ImportColumn("DT")] public DateTime DT { get; set; }
        [ImportColumn("DTO")] public DateTimeOffset DTO { get; set; }
        [ImportColumn("TS")] public TimeSpan TS { get; set; }
        [ImportColumn("G")] public Guid G { get; set; }
        [ImportColumn("E")] public DeviceStatus E { get; set; }
        [ImportColumn("N")] public int? N { get; set; }
    }

    /// <summary>非字面量导入实体（无转换器声明的集合属性，字面量限制负例）。</summary>
    public class NonLiteralImportDto
    {
        [ImportColumn("标签")]
        public List<int> Tags { get; set; }
    }

    /// <summary>非字面量导出实体（无转换器声明的字典属性，字面量限制负例）。</summary>
    public class NonLiteralExportDto
    {
        [ExportColumn("标签")]
        public Dictionary<string, int> Values { get; set; }
    }

    /// <summary>无特性声明的集合属性实体（自定义映射补充列与程序化导出列的字面量限制负例）。</summary>
    public class UnmappedTagsDto
    {
        public List<string> Tags { get; set; }
    }

    /// <summary>无特性声明的多类型实体（程序化导入列的完整解析行为验证）。</summary>
    public class UnmappedFieldsDto
    {
        public DateTime MadeAt { get; set; }

        public int Count { get; set; }

        public List<string> Tags { get; set; }
    }

    /// <summary>集合属性 + 转换器实体（非字面量类型的转换器豁免通道，导入导出共用）。</summary>
    public class StringListDto
    {
        [ImportColumn("标签", ConverterType = typeof(StringListImportConverter))]
        [ExportColumn("标签", ConverterType = typeof(StringListExportConverter))]
        public List<string> Tags { get; set; }
    }

    /// <summary>导入转换器（逗号分隔文本转字符串列表）。</summary>
    public class StringListImportConverter : IImportValueConverter
    {
        public object Convert(string value)
        {
            return new List<string>(value.Split(','));
        }
    }

    /// <summary>导出转换器（字符串列表转逗号分隔文本）。</summary>
    public class StringListExportConverter : IExportValueConverter
    {
        public object Convert(object value)
        {
            return string.Join(",", (List<string>)value);
        }
    }

    /// <summary>格式化解析测试实体（验证 Format 指定的精确解析路径）。</summary>
    public class FormattedDto
    {
        [ImportColumn("日期", Format = "yyyyMMdd")]
        public DateTime Day { get; set; }

        [ImportColumn("价格", Format = "0.00")]
        public double Price { get; set; }
    }

    /// <summary>补充类型导出实体（long/decimal/float 与格式化浮点，覆盖类型写入分支）。</summary>
    public class ExtendedTypesDto
    {
        [ExportColumn("长整型")]
        public long L { get; set; }

        [ExportColumn("金额")]
        public decimal M { get; set; }

        [ExportColumn("浮点")]
        public float F { get; set; }

        [ExportColumn("格式化浮点", Format = "0.00")]
        public float FF { get; set; }

        [ExportColumn("有符号小整型")]
        public sbyte SB { get; set; }

        [ExportColumn("无符号短整型")]
        public ushort US { get; set; }

        [ExportColumn("无符号整型")]
        public uint UI { get; set; }

        [ExportColumn("无符号长整型")]
        public ulong UL { get; set; }

        [ExportColumn("时长")]
        public TimeSpan TS { get; set; }

        [ExportColumn("带偏移日期")]
        public DateTimeOffset DTO { get; set; }
    }

    /// <summary>模板示例行类型实体（bool 列无默认值，验证类型默认示例值）。</summary>
    public class BoolTemplateDto
    {
        [ImportColumn("开关")]
        public bool On { get; set; }
    }

    /// <summary>object 默认值实体（验证类型兼容字面量直用与字符串声明解析）。</summary>
    public class ObjectDefaultDto
    {
        [ImportColumn("数量", DefaultValue = 100)]
        public int Count { get; set; }

        [ImportColumn("状态", DefaultValue = DeviceStatus.Online)]
        public DeviceStatus Status { get; set; }

        [ImportColumn("备注", DefaultValue = "文本")]
        public string Note { get; set; }
    }

    /// <summary>状态码转换器（特性 ConverterType 测试夹具，1 在线、2 离线、其他未知）。</summary>
    public class StatusCodeConverter : IExportValueConverter
    {
        public object Convert(object value)
        {
            var code = (int)value;
            if (code == 1) return "在线";
            if (code == 2) return "离线";
            return "未知";
        }
    }

    /// <summary>未实现 IExportValueConverter 的类型（负例夹具）。</summary>
    public class NonConverterType
    {
    }

    /// <summary>仅带参构造的转换器（负例夹具，验证无参构造校验）。</summary>
    public class NoParameterlessCtorConverter : IExportValueConverter
    {
        public NoParameterlessCtorConverter(int seed) { }

        public object Convert(object value)
        {
            return value;
        }
    }

    /// <summary>特性声明转换器导出实体（状态码列声明 ConverterType）。</summary>
    public class ConverterDto
    {
        [ExportColumn("编码", Order = 1)]
        public string Code { get; set; }

        [ExportColumn("状态", Order = 2, ConverterType = typeof(StatusCodeConverter))]
        public int Status { get; set; }
    }

    /// <summary>非法转换器声明实体（未实现接口，负例）。</summary>
    public class InvalidConverterDto
    {
        [ExportColumn("列", ConverterType = typeof(NonConverterType))]
        public string Value { get; set; }
    }

    /// <summary>缺无参构造转换器声明实体（负例）。</summary>
    public class NoCtorConverterDto
    {
        [ExportColumn("列", ConverterType = typeof(NoParameterlessCtorConverter))]
        public string Value { get; set; }
    }

    /// <summary>状态解析转换器（导入 ConverterType 测试夹具，显示名转状态码，与 StatusCodeConverter 互逆）。</summary>
    public class StatusParsingConverter : IImportValueConverter
    {
        public object Convert(string value)
        {
            if (value == "在线") return 1;
            if (value == "离线") return 2;
            return 9;
        }
    }

    /// <summary>特性声明转换器导入实体（状态列声明 ConverterType）。</summary>
    public class ImportConverterDto
    {
        [ImportColumn("编码", Order = 1)]
        public string Code { get; set; }

        [ImportColumn("状态", Order = 2, ConverterType = typeof(StatusParsingConverter))]
        public int Status { get; set; }
    }

    /// <summary>非法导入转换器声明实体（未实现接口，负例）。</summary>
    public class InvalidImportConverterDto
    {
        [ImportColumn("列", ConverterType = typeof(NonConverterType))]
        public string Value { get; set; }
    }

    /// <summary>仅带参构造的导入转换器（负例夹具，验证导入侧无参构造校验）。</summary>
    public class NoParameterlessCtorImportConverter : IImportValueConverter
    {
        public NoParameterlessCtorImportConverter(int seed) { }

        public object Convert(string value)
        {
            return value;
        }
    }

    /// <summary>缺无参构造导入转换器声明实体（负例）。</summary>
    public class NoCtorImportConverterDto
    {
        [ImportColumn("列", ConverterType = typeof(NoParameterlessCtorImportConverter))]
        public string Value { get; set; }
    }

    /// <summary>只读属性导入实体（负例，缺少 setter）。</summary>
    public class ReadOnlyPropDto
    {
        [ImportColumn("编码")]
        public string Code { get; }
    }

    /// <summary>无特性只读属性实体（程序化导入列绑定不可写属性的负例）。</summary>
    public class UnmappedReadOnlyDto
    {
        public string Code => "D001";
    }

    /// <summary>只写属性导出实体（负例，缺少 getter）。</summary>
    public class WriteOnlyPropDto
    {
        private string code;

        [ExportColumn("编码")]
        public string Code
        {
            set { code = value; }
        }
    }

    /// <summary>无特性只写属性实体（程序化导出列绑定不可读属性的负例）。</summary>
    public class UnmappedWriteOnlyDto
    {
        private string code;

        public string Code
        {
            set { code = value; }
        }
    }

    /// <summary>get-only 导出实体（正例，计算属性导出合法）。</summary>
    public class GetOnlyExportDto
    {
        [ExportColumn("计算值")]
        public string Computed => "x";
    }

    /// <summary>公式注入防护测试实体（危险前缀字符串列）。</summary>
    public class InjectionDto
    {
        [ExportColumn("公式")]
        public string Formula { get; set; }

        [ExportColumn("加号")]
        public string Plus { get; set; }

        [ExportColumn("减号")]
        public string Minus { get; set; }

        [ExportColumn("艾特")]
        public string At { get; set; }

        [ExportColumn("普通")]
        public string Normal { get; set; }
    }

    /// <summary>[Flags] 权限枚举（验证组合值导入解析）。</summary>
    [Flags]
    public enum Permissions
    {
        None = 0,
        Read = 1,
        Write = 2,
        Manage = 4
    }

    /// <summary>[Flags] 枚举导入实体（验证组合名与位掩码数值解析路径）。</summary>
    public class FlagsDto
    {
        [ImportColumn("权限")]
        public Permissions Rights { get; set; }
    }

    /// <summary>必填校验测试实体（无默认值必填列、带默认值必填列、非必填列对照）。</summary>
    public class RequiredDto
    {
        [ImportColumn("编码", IsRequired = true)]
        public string Code { get; set; }

        [ImportColumn("名称", IsRequired = true, DefaultValue = "未命名")]
        public string Name { get; set; }

        [ImportColumn("备注")]
        public string Note { get; set; }
    }

    /// <summary>简单双列导入测试实体（表头按主名匹配）。</summary>
    public class SimpleDto
    {
        [ImportColumn("设备编码")]
        public string Code { get; set; }

        [ImportColumn("设备名称")]
        public string Name { get; set; }
    }

    /// <summary>抛异常导入转换器（转换器执行失败的负例夹具，验证三种错误模式分派）。</summary>
    public class ThrowingImportConverter : IImportValueConverter
    {
        public object Convert(string value)
        {
            throw new InvalidOperationException("转换器内部失败");
        }
    }

    /// <summary>返回类型不匹配的导入转换器（恒返回 double，验证转换器返回值类型守卫）。</summary>
    public class WrongTypeImportConverter : IImportValueConverter
    {
        public object Convert(string value)
        {
            return double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>返回装箱 int 的导入转换器（可空目标属性的类型兼容守卫正例）。</summary>
    public class BoxedIntImportConverter : IImportValueConverter
    {
        public object Convert(string value)
        {
            return int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>转换器返回类型不匹配导入实体（数量列转换器恒返回 double 给 int 属性）。</summary>
    public class ConverterTypeMismatchDto
    {
        [ImportColumn("数量", ConverterType = typeof(WrongTypeImportConverter))]
        public int Count { get; set; }
    }

    /// <summary>可空数值转换器导入实体（转换器返回装箱 int 给 int? 属性）。</summary>
    public class NullableConverterDto
    {
        [ImportColumn("数量", ConverterType = typeof(BoxedIntImportConverter))]
        public int? Count { get; set; }
    }

    /// <summary>抛异常转换器导入实体（数量列声明抛异常转换器）。</summary>
    public class ConverterThrowDto
    {
        [ImportColumn("数量", ConverterType = typeof(ThrowingImportConverter))]
        public int Count { get; set; }
    }

    /// <summary>字符串声明默认值实体（验证字符串默认值的解析路径与不兼容转换）。</summary>
    public class TextDefaultDto
    {
        [ImportColumn("数量", DefaultValue = "100")]
        public int Count { get; set; }

        [ImportColumn("编码", DefaultValue = 123)]
        public string Code { get; set; }

        [ImportColumn("价格", DefaultValue = "abc")]
        public double Price { get; set; }
    }

    /// <summary>导出转换器直接返回 null（导出侧转换器空返回的落单元格行为）。</summary>
    public class NullReturningExportConverter : IExportValueConverter
    {
        public object Convert(object value)
        {
            return null;
        }
    }

    /// <summary>转换器返回 null 的导出实体。</summary>
    public class NullConverterExportDto
    {
        [ExportColumn("编码", ConverterType = typeof(NullReturningExportConverter))]
        public string Code { get; set; }
    }
}
