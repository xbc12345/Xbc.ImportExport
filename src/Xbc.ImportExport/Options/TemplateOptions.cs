namespace Xbc.ImportExport
{
    /// <summary>模板生成选项。</summary>
    public sealed class TemplateOptions
    {
        /// <summary>包含示例行（表头下 1 行，展示各列格式与取值风格）。</summary>
        public bool IncludeExampleRows { get; set; } = true;

        /// <summary>包含字段说明 Sheet（每个字段的含义、格式、是否必填）。</summary>
        public bool IncludeFieldDescription { get; set; } = true;

        /// <summary>数据校验下拉（枚举字段生成下拉列表）。</summary>
        public bool IncludeDataValidation { get; set; } = true;

        /// <summary>Sheet 名称（Excel 模板用，默认 "导入模板"；CSV 模板无 Sheet 概念，忽略此项）。</summary>
        public string SheetName { get; set; } = "导入模板";
    }
}
