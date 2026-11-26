using ClosedXML.Excel;
using Xbc.ImportExport.Excel;

namespace Xbc.ImportExport.Tests
{
    public class ExcelTemplateTests
    {
        private readonly ExcelImporter importer = new ExcelImporter();

        private static async Task<XLWorkbook> GenerateWorkbookAsync(IImporter importer, TemplateOptions options)
        {
            var stream = await importer.GenerateTemplateAsync<DeviceDto>(options);
            return new XLWorkbook(stream);
        }

        [Fact]
        public async Task Generate_BasicTemplate_HeadersStyledAndOrdered()
        {
            using (var workbook = await GenerateWorkbookAsync(importer, new TemplateOptions()))
            {
                Assert.Equal(2, workbook.Worksheets.Count);
                var ws = workbook.Worksheet("导入模板");

                Assert.Equal("设备编码", ws.Cell(1, 1).GetString());
                Assert.Equal("备注数", ws.Cell(1, 9).GetString());
                Assert.True(ws.Cell(1, 1).Style.Font.Bold);
                Assert.Equal(XLColor.LightBlue, ws.Cell(1, 1).Style.Fill.BackgroundColor);
                // DeviceDto 无必填列，全部表头非红色（必填标记见 RequiredDto 专项测试）
                Assert.NotEqual(XLColor.Red, ws.Cell(1, 1).Style.Font.FontColor);
            }
        }

        [Fact]
        public async Task Generate_SheetNameOver31Chars_ThrowsTemplateException()
        {
            // Excel 工作表名上限 31 字符，超限前置校验给中文错误（与 ExcelExporter.AddSheet 对称）
            var options = new TemplateOptions { SheetName = new string('长', 32) };

            var ex = await Assert.ThrowsAsync<TemplateException>(() => importer.GenerateTemplateAsync<DeviceDto>(options));
            Assert.Contains("31", ex.Message);
        }

        [Fact]
        public async Task Generate_ExampleRow_HasCommentHint()
        {
            // 示例行首格挂批注，明示填写前删除示例行（避免示例数据混入真实导入）
            using (var workbook = await GenerateWorkbookAsync(importer, new TemplateOptions { IncludeExampleRows = true }))
            {
                var cell = workbook.Worksheet("导入模板").Cell(2, 1);

                Assert.True(cell.HasComment);
                Assert.Contains("示例行", cell.GetComment().Text);
            }
        }

        [Fact]
        public async Task Generate_RequiredColumns_MarkedRed()
        {
            // RequiredDto：编码/名称必填红色标记，备注非必填不标记
            var stream = await importer.GenerateTemplateAsync<RequiredDto>(new TemplateOptions());
            using (var workbook = new XLWorkbook(stream))
            {
                var ws = workbook.Worksheet("导入模板");

                Assert.Equal(XLColor.Red, ws.Cell(1, 1).Style.Font.FontColor);
                Assert.Equal(XLColor.Red, ws.Cell(1, 2).Style.Font.FontColor);
                Assert.NotEqual(XLColor.Red, ws.Cell(1, 3).Style.Font.FontColor);
            }
        }

        [Fact]
        public async Task Generate_ColumnWidth_FromImportColumnAttribute()
        {
            using (var workbook = await GenerateWorkbookAsync(importer, new TemplateOptions()))
            {
                var ws = workbook.Worksheet("导入模板");

                Assert.Equal(30, ws.Column(1).Width);
                Assert.Equal(15, ws.Column(2).Width);
            }
        }

        [Fact]
        public async Task Generate_EnumColumn_CreatesListValidation()
        {
            using (var workbook = await GenerateWorkbookAsync(importer, new TemplateOptions()))
            {
                var ws = workbook.Worksheet("导入模板");

                var validation = Assert.Single(ws.DataValidations);
                Assert.Equal("\"Online,Offline,Unknown\"", validation.Value);
                Assert.Equal("输入无效", validation.ErrorTitle);
                Assert.Equal(XLErrorStyle.Warning, validation.ErrorStyle);
            }
        }

        [Fact]
        public async Task Generate_LongAllowedValues_ListReferencesHiddenSheet()
        {
            // 字面列表超 Excel 数据校验公式 255 字符上限：改写隐藏工作表『下拉选项』的引用区域，Excel 打开不再剥离校验
            var stream = await importer.GenerateTemplateAsync<LongEnumTemplateDto>(new TemplateOptions());
            using (var workbook = new XLWorkbook(stream))
            {
                var ws = workbook.Worksheet("导入模板");

                var validation = Assert.Single(ws.DataValidations);
                Assert.Equal("'下拉选项'!$A$1:$A$20", validation.Value);

                var listSheet = workbook.Worksheet("下拉选项");
                Assert.Equal(XLWorksheetVisibility.Hidden, listSheet.Visibility);
                Assert.Equal("AlphaOption00000000000000001", listSheet.Cell(1, 1).GetString());
                Assert.Equal("AlphaOption00000000000000020", listSheet.Cell(20, 1).GetString());
            }
        }

        [Fact]
        public async Task Generate_ExampleRows_WritesFallbackValuesWithGrayItalicStyle()
        {
            using (var workbook = await GenerateWorkbookAsync(importer, new TemplateOptions()))
            {
                var ws = workbook.Worksheet("导入模板");

                Assert.Equal("示例文本", ws.Cell(2, 1).GetString());
                Assert.Equal("true", ws.Cell(2, 3).GetString());
                Assert.Equal("100", ws.Cell(2, 4).GetString());
                Assert.Equal("Online", ws.Cell(2, 8).GetString());
                Assert.True(ws.Cell(2, 1).Style.Font.Italic);
                Assert.Equal(XLColor.Gray, ws.Cell(2, 1).Style.Font.FontColor);
                Assert.True(ws.Cell(3, 1).IsEmpty());
            }
        }

        [Fact]
        public async Task Generate_ExampleRows_BoolColumnWithoutDefault_UsesTypeDefault()
        {
            var stream = await importer.GenerateTemplateAsync<BoolTemplateDto>(new TemplateOptions());

            using (var workbook = new XLWorkbook(stream))
            {
                var ws = workbook.Worksheet("导入模板");
                Assert.Equal("true", ws.Cell(2, 1).GetString());
            }
        }

        [Fact]
        public async Task Generate_TimeSpanColumn_ExampleUsesTimeSpanFormat()
        {
            // TimeSpan 列数据类型归为 String，示例行单独按时间间隔格式展示（"示例文本"会误导填写方）
            var stream = await importer.GenerateTemplateAsync<TypesDto>(new TemplateOptions());

            using (var workbook = new XLWorkbook(stream))
            {
                var ws = workbook.Worksheet("导入模板");
                var tsHeader = ws.Row(1).Cells().First(c => c.GetString() == "TS");
                Assert.Equal("01:02:03", ws.Cell(2, tsHeader.Address.ColumnNumber).GetString());
            }
        }

        [Fact]
        public async Task Generate_FieldDescriptionSheet_ContainsColumnMetadata()
        {
            using (var workbook = await GenerateWorkbookAsync(importer, new TemplateOptions()))
            {
                var desc = workbook.Worksheet("字段说明");

                Assert.Equal("列名", desc.Cell(1, 1).GetString());
                Assert.Equal("是否必填", desc.Cell(1, 2).GetString());
                Assert.Equal("数据类型", desc.Cell(1, 3).GetString());
                Assert.Equal("格式说明", desc.Cell(1, 4).GetString());
                Assert.Equal("允许值", desc.Cell(1, 5).GetString());
                Assert.Equal("说明", desc.Cell(1, 6).GetString());

                Assert.Equal("设备编码", desc.Cell(2, 1).GetString());
                Assert.Equal("否", desc.Cell(2, 2).GetString());
                Assert.Equal("String", desc.Cell(2, 3).GetString());

                Assert.Equal("设备名称", desc.Cell(3, 1).GetString());
                Assert.Equal("设备显示名称", desc.Cell(3, 6).GetString());

                Assert.Equal("状态", desc.Cell(9, 1).GetString());
                Assert.Equal("否", desc.Cell(9, 2).GetString());
                Assert.Equal("Enum", desc.Cell(9, 3).GetString());
                Assert.Equal("Online, Offline, Unknown", desc.Cell(9, 5).GetString());
            }
        }

        [Fact]
        public async Task Generate_IncludeExampleRowsFalse_NoExampleRows()
        {
            var options = new TemplateOptions { IncludeExampleRows = false };

            using (var workbook = await GenerateWorkbookAsync(importer, options))
            {
                var ws = workbook.Worksheet("导入模板");

                Assert.True(ws.Cell(2, 1).IsEmpty());
                Assert.True(ws.Cell(3, 1).IsEmpty());
            }
        }

        [Fact]
        public async Task Generate_IncludeFieldDescriptionFalse_NoDescriptionSheet()
        {
            var options = new TemplateOptions { IncludeFieldDescription = false };

            using (var workbook = await GenerateWorkbookAsync(importer, options))
            {
                Assert.Equal(1, workbook.Worksheets.Count);
                Assert.DoesNotContain(workbook.Worksheets, w => w.Name == "字段说明");
            }
        }

        [Fact]
        public async Task Generate_IncludeDataValidationFalse_NoValidations()
        {
            var options = new TemplateOptions { IncludeDataValidation = false };

            using (var workbook = await GenerateWorkbookAsync(importer, options))
            {
                var ws = workbook.Worksheet("导入模板");

                Assert.Empty(ws.DataValidations);
            }
        }

        [Fact]
        public async Task Generate_CustomSheetName_Applied()
        {
            var options = new TemplateOptions { SheetName = "设备导入模板" };

            using (var workbook = await GenerateWorkbookAsync(importer, options))
            {
                Assert.NotNull(workbook.Worksheet("设备导入模板"));
            }
        }

        [Fact]
        public async Task Generate_NoImportColumns_ThrowsTemplateException()
        {
            var exception = await Assert.ThrowsAsync<TemplateException>(() =>
                importer.GenerateTemplateAsync<PlainDto>(new TemplateOptions()));

            Assert.Contains("未声明导入列", exception.Message);
        }

        [Fact]
        public async Task Generate_NullOptions_UsesDefaults()
        {
            using (var workbook = await GenerateWorkbookAsync(importer, null))
            {
                Assert.NotNull(workbook.Worksheet("导入模板"));
                Assert.NotNull(workbook.Worksheet("字段说明"));
            }
        }

        [Fact]
        public async Task Generate_ReturnedStream_PositionResetToZero()
        {
            var stream = await importer.GenerateTemplateAsync<DeviceDto>(new TemplateOptions());

            Assert.Equal(0, stream.Position);
        }

        [Fact]
        public async Task Generate_CancelledToken_ThrowsOperationCanceledException()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await importer.GenerateTemplateAsync<DeviceDto>(new TemplateOptions(), cts.Token));
        }
    }
}
