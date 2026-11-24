namespace Xbc.ImportExport
{
    /// <summary>导出列映射特性（声明实体属性与导出列的对应关系；未标记的属性不导出）。</summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class ExportColumnAttribute : Attribute
    {
        /// <summary>列名（表头显示名，默认使用属性名）。</summary>
        public string Name { get; set; }

        /// <summary>列顺序（从 1 开始，0 表示按声明顺序）。</summary>
        public int Order { get; set; }

        /// <summary>格式化（日期/数字格式，如 "yyyy-MM-dd" 或 "N2"）。</summary>
        public string Format { get; set; }

        /// <summary>列宽（Excel 导出的列宽控制，单位为字符宽度；CSV 导出不渲染）。</summary>
        public double Width { get; set; } = 15;

        /// <summary>值转换器类型（须实现 <see cref="IExportValueConverter"/> 且具有无参构造函数）。</summary>
        public Type ConverterType { get; set; }

        /// <summary>初始化导出列映射特性。</summary>
        public ExportColumnAttribute(string name = null)
        {
            Name = name ?? string.Empty;
        }
    }
}
