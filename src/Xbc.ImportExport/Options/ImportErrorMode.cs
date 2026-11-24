namespace Xbc.ImportExport
{
    /// <summary>导入错误处理模式。</summary>
    public enum ImportErrorMode
    {
        /// <summary>宽容模式（默认），值解析失败静默回退类型默认值，不产生错误记录。</summary>
        Lenient = 0,

        /// <summary>严格模式，任一值解析失败立即抛出 <see cref="ImportDataException"/>（定位行号与列名），终止导入。</summary>
        Strict = 1,

        /// <summary>收集模式，值解析失败记录到 <see cref="ParsedRow{T}.Errors"/> 并回退类型默认值，继续产出该行。</summary>
        Collect = 2
    }
}
