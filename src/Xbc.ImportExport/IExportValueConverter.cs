namespace Xbc.ImportExport
{
    /// <summary>导出值转换器（用于 <see cref="ExportColumnAttribute.ConverterType"/>，须有无参构造且无状态）。</summary>
    public interface IExportValueConverter
    {
        /// <summary>转换单元格值（仅非空属性值调用，null 值由导出器直接写空单元格）。</summary>
        object Convert(object value);
    }
}
