namespace Xbc.ImportExport
{
    /// <summary>导入值转换器（用于 <see cref="ImportColumnAttribute.ConverterType"/>，须有无参构造且无状态）。</summary>
    public interface IImportValueConverter
    {
        /// <summary>转换单元格文本值（仅非空值调用，空值走默认值路径）。</summary>
        object Convert(string value);
    }
}
