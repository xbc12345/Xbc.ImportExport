using System.Linq.Expressions;

namespace Xbc.ImportExport
{
    /// <summary>导出选项。</summary>
    public class ExportOptions
    {
        /// <summary>是否包含标题行（默认 true）。</summary>
        public bool IncludeHeader { get; set; } = true;

        /// <summary>Sheet 名称（Excel 导出用，默认 "Sheet1"；CSV 导出无 Sheet 概念，忽略此项）。</summary>
        public string SheetName { get; set; } = "Sheet1";

        /// <summary>导出数据量上限（防止 OOM，默认 100000；小于等于 0 表示不限，超过上限抛 <see cref="ExportException"/>）。</summary>
        public int MaxRows { get; set; } = 100000;

        /// <summary>单工作表数据行上限（仅 Excel 导出生效，0 = 不分片，默认 0；超出后自动新建工作表，命名为 基名/基名-2/基名-3…，每个工作表重复表头与列宽；基名过长致分片名超过 31 字符时由 Excel 导出器抛出；CSV 忽略此项）。</summary>
        public int MaxRowsPerSheet { get; set; }

        /// <summary>程序化导出列（列名 + 属性 + 格式/列宽/转换器的完整导出定义；与特性声明列择一生效，Count > 0 时完全替代特性列）。</summary>
        public List<ExportColumn> Columns { get; set; } = new List<ExportColumn>();

        /// <summary>列变换回调（特性声明列与程序化列均逐列经过）：接收解析完成列定义的克隆副本，
        /// 返回替换定义（可安全改写元数据与 <see cref="ExportColumn.Converter"/>，如脱敏包装）或原列，
        /// 返回 null 表示保留原列；逐列语义，无法删列/重排/注入，不影响类型缓存与调用方列实例。</summary>
        public Func<ExportColumn, ExportColumn> ColumnTransformer { get; set; }
    }

    /// <summary>强类型导出选项（通过属性选择器声明导出列，追加到 <see cref="ExportOptions.Columns"/>）。</summary>
    public sealed class ExportOptions<T> : ExportOptions
    {
        /// <summary>声明导出列（属性选择器 → 列名，可链式调用；列名留空则用属性名，configure 可进一步设置格式/列宽/转换器）。</summary>
        public ExportOptions<T> Column<TValue>(
            Expression<Func<T, TValue>> selector,
            string name = null,
            Action<ExportColumn> configure = null)
        {
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

            var property = PropertySelectorParser.GetProperty(selector);
            var column = new ExportColumn
            {
                PropertyName = property.Name,
                Property = property,
                Name = string.IsNullOrEmpty(name) ? property.Name : name
            };

            configure?.Invoke(column);
            Columns.Add(column);
            return this;
        }
    }
}
