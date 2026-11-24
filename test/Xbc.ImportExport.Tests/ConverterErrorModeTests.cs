using Xbc.ImportExport.Csv;

namespace Xbc.ImportExport.Tests
{
    /// <summary>
    /// 转换器执行失败的错误模式分派与声明默认值的字符串解析路径（经 CSV 导入器端到端验证）。
    /// </summary>
    public class ConverterErrorModeTests
    {
        private readonly CsvImporter importer = new CsvImporter();

        [Fact]
        public async Task ConverterThrows_LenientMode_SilentlyFallsBackToTypeDefault()
        {
            // 宽容模式：转换器异常静默回退类型默认值，不产生错误记录
            var content = "数量\n5";

            var rows = await TestHelpers.ImportAllAsync<ConverterThrowDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal(0, row.Data.Count);
            Assert.Empty(row.Errors);
        }

        [Fact]
        public async Task ConverterThrows_StrictMode_ThrowsImportDataExceptionWithInnerAndColumn()
        {
            // 严格模式：抛出数据异常并定位行号与列名，原始异常作为内部异常保留
            var content = "数量\n5";

            var exception = await Assert.ThrowsAsync<ImportDataException>(
                async () => await TestHelpers.ImportAllAsync<ConverterThrowDto>(
                    importer, TestHelpers.ToStream(content), new ImportOptions { ErrorMode = ImportErrorMode.Strict }));

            Assert.Equal(2, exception.RowNumber);
            Assert.Equal("数量", exception.ColumnName);
            Assert.Contains("转换器执行失败", exception.Message);
            Assert.IsType<InvalidOperationException>(exception.InnerException);
        }

        [Fact]
        public async Task ConverterThrows_CollectMode_RecordsErrorAndContinues()
        {
            // 收集模式：错误记录到行 Errors 且不拦截行产出（转换器恒抛异常，每行都有错误但行照常输出）
            var content = "数量\n5\n6";

            var rows = await TestHelpers.ImportAllAsync<ConverterThrowDto>(
                importer, TestHelpers.ToStream(content), new ImportOptions { ErrorMode = ImportErrorMode.Collect });

            Assert.Equal(2, rows.Count);
            Assert.Contains("转换器执行失败", rows[0].Errors.Single());
            Assert.Contains("转换器执行失败", rows[1].Errors.Single());
        }

        [Fact]
        public async Task StringDeclaredDefault_ParsesToTargetType()
        {
            // 字符串声明默认值按解析路径转换（"100" → int 100）；全空行需显式保留（默认跳过）
            var content = "数量,编码,价格\n,";
            var options = new ImportOptions { SkipEmptyRows = false };

            var rows = await TestHelpers.ImportAllAsync<TextDefaultDto>(importer, TestHelpers.ToStream(content), options);

            var row = Assert.Single(rows);
            Assert.Equal(100, row.Data.Count);
        }

        [Fact]
        public async Task IncompatibleDeclaredDefault_ConvertsViaTextPath()
        {
            // 数值声明默认值给字符串属性：不兼容转文本解析（123 → "123"）
            var content = "数量,编码,价格\n,";
            var options = new ImportOptions { SkipEmptyRows = false };

            var rows = await TestHelpers.ImportAllAsync<TextDefaultDto>(importer, TestHelpers.ToStream(content), options);

            var row = Assert.Single(rows);
            Assert.Equal("123", row.Data.Code);
        }

        [Fact]
        public async Task UnparsableDeclaredDefault_FallsBackToTypeDefault()
        {
            // 声明默认值解析失败（"abc" → double）静默回退类型默认值 0，即使 Strict 模式也不抛出
            var content = "数量,编码,价格\n,";
            var options = new ImportOptions { SkipEmptyRows = false, ErrorMode = ImportErrorMode.Strict };

            var rows = await TestHelpers.ImportAllAsync<TextDefaultDto>(importer, TestHelpers.ToStream(content), options);

            var row = Assert.Single(rows);
            Assert.Equal(0, row.Data.Price);
            Assert.Empty(row.Errors);
        }

        [Fact]
        public async Task ConverterReturnTypeMismatch_LenientMode_SilentlyFallsBackToTypeDefault()
        {
            // 宽容模式：转换器返回值与属性类型不兼容（double → int）静默回退类型默认值，不产生错误记录
            var content = "数量\n5";

            var rows = await TestHelpers.ImportAllAsync<ConverterTypeMismatchDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal(0, row.Data.Count);
            Assert.Empty(row.Errors);
        }

        [Fact]
        public async Task ConverterReturnTypeMismatch_StrictMode_ThrowsImportDataExceptionWithLocation()
        {
            // 严格模式：抛出数据异常定位行号与列名，消息携带返回类型与目标类型
            //（守卫前的行为是裸 ArgumentException 被外层包装成无列名异常，定位全丢）
            var content = "数量\n5";

            var exception = await Assert.ThrowsAsync<ImportDataException>(
                async () => await TestHelpers.ImportAllAsync<ConverterTypeMismatchDto>(
                    importer, TestHelpers.ToStream(content), new ImportOptions { ErrorMode = ImportErrorMode.Strict }));

            Assert.Equal(2, exception.RowNumber);
            Assert.Equal("数量", exception.ColumnName);
            Assert.Contains("转换器返回了", exception.Message);
            Assert.Contains("System.Double", exception.Message);
            Assert.Contains("System.Int32", exception.Message);
        }

        [Fact]
        public async Task ConverterReturnTypeMismatch_CollectMode_RecordsErrorAndContinues()
        {
            // 收集模式：错误记录到行 Errors 且不拦截行产出
            var content = "数量\n5\n6";

            var rows = await TestHelpers.ImportAllAsync<ConverterTypeMismatchDto>(
                importer, TestHelpers.ToStream(content), new ImportOptions { ErrorMode = ImportErrorMode.Collect });

            Assert.Equal(2, rows.Count);
            Assert.Contains("无法赋给", rows[0].Errors.Single());
            Assert.Contains("无法赋给", rows[1].Errors.Single());
        }

        [Fact]
        public async Task ConverterReturnsBoxedValueForNullableProperty_AssignsNormally()
        {
            // 可空目标属性的类型兼容正例：转换器返回装箱 int（与 int? 的底层类型运行时等价）正常赋值，
            // 锁定守卫的可空解包判定（未解包则 typeof(int?).IsInstanceOfType(装箱 int) 误判不兼容）
            var content = "数量\n5";

            var rows = await TestHelpers.ImportAllAsync<NullableConverterDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal(5, row.Data.Count);
            Assert.Empty(row.Errors);
        }
    }
}
