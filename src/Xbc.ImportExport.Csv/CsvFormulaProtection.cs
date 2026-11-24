namespace Xbc.ImportExport.Csv
{
    /// <summary>
    /// CSV 公式注入防护（导出与错误回执共用）：首字符为 =、+、-、@、制表符或回车的文本，Excel 打开 CSV 时会尝试解释为公式，
    /// 加 ' 前缀防解释；' 为 CSV 字面字符，RFC 4180 转义由 CsvWriter 处理，此处仅防公式注入。
    /// </summary>
    internal static class CsvFormulaProtection
    {
        /// <summary>公式触发文本加 ' 前缀，非触发文本与空值原样返回。</summary>
        public static string EscapeFormula(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            var c = value[0];
            return c == '=' || c == '+' || c == '-' || c == '@' || c == '\t' || c == '\r'
                ? "'" + value
                : value;
        }
    }
}
