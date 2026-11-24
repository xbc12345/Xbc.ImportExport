namespace Xbc.ImportExport
{
    /// <summary>导入列映射特性（声明实体属性与导入列的对应关系）。</summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class ImportColumnAttribute : Attribute
    {
        /// <summary>列名（表头显示名，默认使用属性名）。</summary>
        public string Name { get; set; }

        /// <summary>格式说明（如 "yyyy-MM-dd"）。</summary>
        public string Format { get; set; }

        /// <summary>默认值（单元格为空时使用；支持类型兼容的字面量）。</summary>
        public object DefaultValue { get; set; }

        /// <summary>值转换器类型（须实现 <see cref="IImportValueConverter"/> 且具有无参构造函数）。</summary>
        public Type ConverterType { get; set; }

        /// <summary>
        /// 是否必填（Excel 模板的表头红色标记与字段说明，CSV 模板不渲染；
        /// 导入必填校验：空单元格且未声明默认值按错误模式分派，Lenient 静默回退类型默认值/Strict 抛出/Collect 记录）。
        /// </summary>
        public bool IsRequired { get; set; }

        // ---- 以下为模板专用属性（导入绑定不读取）----

        /// <summary>列序（从 1 开始，0 表示按声明顺序；排序模板列布局与导入列映射，导入绑定按列名匹配、结果与顺序无关）。</summary>
        public int Order { get; set; }

        /// <summary>列宽（Excel 模板的列宽控制，单位为字符宽度；CSV 模板不渲染）。</summary>
        public double Width { get; set; } = 15;

        /// <summary>列说明（展示于 Excel 模板的字段说明 Sheet；CSV 模板不渲染）。</summary>
        public string Description { get; set; }

        /// <summary>初始化导入列映射特性。</summary>
        public ImportColumnAttribute(string name = null)
        {
            Name = name ?? string.Empty;
        }
    }
}
