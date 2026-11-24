using System.Linq.Expressions;

namespace Xbc.ImportExport
{
    /// <summary>导入选项。</summary>
    public class ImportOptions
    {
        /// <summary>表头所在行号（从 1 开始，默认 1；数据从表头下一行开始读取）。</summary>
        public int StartRow { get; set; } = 1;

        /// <summary>是否跳过空行（默认 true）。</summary>
        public bool SkipEmptyRows { get; set; } = true;

        /// <summary>导入数据量上限（防止 OOM，默认 100000；小于等于 0 表示不限，超过上限抛 <see cref="ImportDataException"/> 并定位行号，与导出侧 MaxRows 语义对称）。</summary>
        public int MaxRows { get; set; } = 100000;

        /// <summary>工作表名称（仅 Excel 导入生效；null 或空 = 第一个工作表，指定名称不存在时抛 <see cref="ImportException"/>；CSV 无工作表概念忽略此项）。</summary>
        public string SheetName { get; set; }

        /// <summary>程序化导入列（列名 + 属性 + 格式/默认值/转换器的完整导入定义；与特性声明列择一生效，Count > 0 时完全替代特性列）。</summary>
        public List<ImportColumn> Columns { get; set; } = new List<ImportColumn>();

        /// <summary>错误处理模式（默认宽容，值解析失败回退类型默认值）。</summary>
        public ImportErrorMode ErrorMode { get; set; } = ImportErrorMode.Lenient;
    }

    /// <summary>强类型导入选项（通过属性选择器声明导入列，追加到 <see cref="ImportOptions.Columns"/>）。</summary>
    public sealed class ImportOptions<T> : ImportOptions
    {
        /// <summary>声明导入列（属性选择器 → 列名，可链式调用；列名留空则用属性名，configure 可进一步设置格式/默认值/转换器）。</summary>
        public ImportOptions<T> Column<TValue>(
            Expression<Func<T, TValue>> selector,
            string columnName = null,
            Action<ImportColumn> configure = null)
        {
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

            var property = PropertySelectorParser.GetProperty(selector);
            var column = new ImportColumn
            {
                PropertyName = property.Name,
                Property = property,
                Name = string.IsNullOrEmpty(columnName) ? property.Name : columnName
            };

            configure?.Invoke(column);
            Columns.Add(column);
            return this;
        }
    }
}
