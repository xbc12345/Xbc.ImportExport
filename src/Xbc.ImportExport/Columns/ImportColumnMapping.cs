namespace Xbc.ImportExport
{
    /// <summary>导入属性映射（绑定列与文件侧列索引，导入器逐行取值绑定的依据）。</summary>
    internal class ImportColumnMapping
    {
        /// <summary>绑定列。</summary>
        public ImportColumn Column { get; set; }

        /// <summary>列索引（随格式库约定：CSV 从 0 开始，Excel 从 1 开始）。</summary>
        public int ColumnIndex { get; set; }
    }
}
