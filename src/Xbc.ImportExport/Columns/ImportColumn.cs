using System.Reflection;

namespace Xbc.ImportExport
{
    /// <summary>导入绑定列（导入器的表头匹配与值绑定信息，特性声明经 <see cref="ImportColumnAttribute"/> 解析填充，程序化列经 <see cref="ImportOptions.Columns"/> 声明并规范化；模板展示信息见 <see cref="TemplateColumn"/>）。</summary>
    public sealed class ImportColumn
    {
        /// <summary>列名（表头匹配名；程序化列留空回退属性名）。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>属性名（实体字段名，程序化列按名回查属性的依据）。</summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>属性元数据（强类型选项声明或列定义解析时填充）。</summary>
        public PropertyInfo Property { get; set; }

        /// <summary>解析格式（如 "yyyy-MM-dd"，空表示默认解析）。</summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>声明默认值（null 表示未声明；类型兼容值直用，字符串按解析路径）。</summary>
        public object DefaultValue { get; set; }

        /// <summary>是否必填（空单元格且未声明默认值时按 <see cref="ImportErrorMode"/> 分派必填缺失：Strict 抛出、Collect 记录、Lenient 静默回退类型默认值不算拦截）。</summary>
        public bool IsRequired { get; set; }

        /// <summary>值转换器（特性声明经 <see cref="ImportColumnAttribute.ConverterType"/> 实例化，程序化列直接指定；仅非空值调用，入参为单元格文本值）。</summary>
        public Func<string, object> Converter { get; set; }
    }
}
