namespace Xbc.ImportExport
{
    /// <summary>模板列数据类型（由属性类型推断，供示例值生成与字段说明使用）。</summary>
    public enum TemplateColumnType
    {
        /// <summary>字符串。</summary>
        String,

        /// <summary>整数（int/uint/short/ushort/byte/sbyte）。</summary>
        Integer,

        /// <summary>长整数（long/ulong）。</summary>
        Long,

        /// <summary>数值（double/float/decimal）。</summary>
        Number,

        /// <summary>布尔值。</summary>
        Boolean,

        /// <summary>日期时间（DateTime/DateTimeOffset）。</summary>
        DateTime,

        /// <summary>GUID。</summary>
        Guid,

        /// <summary>枚举。</summary>
        Enum
    }
}
