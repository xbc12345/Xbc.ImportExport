using System.Reflection;

namespace Xbc.ImportExport
{
    /// <summary>导出列定义。</summary>
    public sealed class ExportColumn
    {
        /// <summary>属性名（实体字段名）。</summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>属性元数据（强类型选项声明或列定义解析时填充，规范化后恒非空）。</summary>
        public PropertyInfo Property { get; set; }

        /// <summary>列名（表头显示名；程序化列留空回退属性名）。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>列顺序（从 1 开始，0 表示按声明顺序）。</summary>
        public int Order { get; set; }

        /// <summary>格式化（日期/数字格式，如 "yyyy-MM-dd" 或 "N2"）。</summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>值转换器（枚举显示、脱敏等；null 属性值不经过转换器，直接落为空单元格）。</summary>
        public Func<object, object> Converter { get; set; }

        /// <summary>列宽（单位为字符宽度，null 表示未指定、保持格式默认列宽）。</summary>
        public double? Width { get; set; }
    }
}
