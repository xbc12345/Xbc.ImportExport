namespace Xbc.ImportExport
{
    /// <summary>模板列定义（导入模板的列布局与展示信息，由 <see cref="ImportColumnAttribute"/> 解析填充，供模板生成器使用）。</summary>
    public sealed class TemplateColumn
    {
        /// <summary>列名（表头显示名）。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>属性名（实体字段名，与 <see cref="ImportColumnAttribute"/> 声明一致）。</summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>是否必填（模板表头红色标记与字段说明）。</summary>
        public bool IsRequired { get; set; }

        /// <summary>数据类型（示例值生成与字段说明）。</summary>
        public TemplateColumnType DataType { get; set; }

        /// <summary>允许的值（枚举/下拉列表，供数据校验下拉、示例行与字段说明使用）。</summary>
        public IList<string> AllowedValues { get; set; } = new List<string>();

        /// <summary>格式说明（如 "yyyy-MM-dd"）。</summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>列宽（单位为字符宽度，默认 15）。</summary>
        public double Width { get; set; } = 15;

        /// <summary>列说明（注释/帮助文字）。</summary>
        public string Comment { get; set; } = string.Empty;

        /// <summary>时间间隔列标记（解析期按属性类型填充；数据类型仍按 String 展示，仅示例值区分）。</summary>
        internal bool IsTimeSpanColumn { get; set; }

        /// <summary>获取列示例值（允许值首个 &gt; 时间间隔格式 &gt; 按数据类型推断；默认值属导入回退语义，不参与示例）。</summary>
        public string GetExampleValue()
        {
            if (AllowedValues != null && AllowedValues.Count > 0)
                return AllowedValues[0];

            // TimeSpan 列数据类型归为 String，通用字符串示例（"示例文本"）会误导填写方，单独按时间间隔格式展示
            if (IsTimeSpanColumn)
                return "01:02:03";

            return GetExampleValue(DataType);
        }

        /// <summary>按数据类型获取默认示例值（列无允许值时使用）。</summary>
        private static string GetExampleValue(TemplateColumnType dataType)
        {
            switch (dataType)
            {
                case TemplateColumnType.String:
                    return "示例文本";
                case TemplateColumnType.Integer:
                case TemplateColumnType.Long:
                    return "100";
                case TemplateColumnType.Number:
                    return "99.99";
                case TemplateColumnType.Boolean:
                    return "true";
                case TemplateColumnType.DateTime:
                    return "2026-01-01";
                case TemplateColumnType.Guid:
                    return "00000000-0000-0000-0000-000000000000";
                case TemplateColumnType.Enum:
                    return "枚举值1";
                default:
                    return string.Empty;
            }
        }
    }
}
