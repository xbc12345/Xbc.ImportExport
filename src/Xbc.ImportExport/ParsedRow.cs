namespace Xbc.ImportExport
{
    /// <summary>解析后的行数据（含行号与原始字典）。</summary>
    public sealed class ParsedRow<T> where T : class, new()
    {
        /// <summary>行号（文件物理行号，从 1 开始；数据行从 StartRow + 1 开始，默认即从 2 开始）。</summary>
        public int RowNumber { get; set; }

        /// <summary>解析后的实体。</summary>
        public T Data { get; set; }

        /// <summary>原始单元格字典（列名 → 字符串值，用于错误定位与 RawData）。</summary>
        public IDictionary<string, string> RawValues { get; set; } = new Dictionary<string, string>();

        /// <summary>行级解析错误（收集模式下值解析失败的描述，空列表表示无错误）。</summary>
        public IList<string> Errors { get; set; } = new List<string>();
    }
}
