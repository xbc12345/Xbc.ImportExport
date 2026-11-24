using Xbc.ImportExport.Csv;

namespace Xbc.ImportExport.Tests
{
    public class CsvExporterTests
    {
        private readonly CsvExporter exporter = new CsvExporter();

        private static DeviceDto CreateDevice()
        {
            return new DeviceDto
            {
                Code = "D001",
                Name = "温度传感器",
                Enabled = true,
                Count = 10,
                Price = 1.5,
                MadeAt = new DateTime(2026, 1, 2),
                Uid = new Guid("00000000-0000-0000-0000-000000000001"),
                Status = DeviceStatus.Online,
                Remark = 5
            };
        }

        [Fact]
        public async Task Export_AttributeColumns_WritesHeaderAndRows()
        {
            var stream = await exporter.ExportAsync(TestHelpers.DataOf(CreateDevice()), new ExportOptions());

            var content = TestHelpers.ReadAll(stream);
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("设备编码,设备名称,启用,数量,价格,生产日期,编号,状态,备注数", lines[0]);
            Assert.Equal("D001,温度传感器,True,10,1.5,2026-01-02,00000000-0000-0000-0000-000000000001,Online,5", lines[1]);
            Assert.Equal(2, lines.Length);
        }

        [Fact]
        public async Task Export_ProgrammaticColumns_OrderSortsHeader()
        {
            // 程序化列 Order 与特性列同样参与排序（Order = 0 按声明顺序排后）
            var options = new ExportOptions<DeviceDto>()
                .Column(x => x.Code, "编码", c => c.Order = 3)
                .Column(x => x.Name, "名称", c => c.Order = 1)
                .Column(x => x.Count, "数量", c => c.Order = 2);

            var stream = await exporter.ExportAsync(TestHelpers.DataOf(CreateDevice()), options);

            var content = TestHelpers.ReadAll(stream);
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("名称,数量,编码", lines[0]);
        }

        [Fact]
        public async Task Export_ProgrammaticColumn_EmptyName_FallsBackToPropertyName()
        {
            // 直填列名留空回退属性名（空名列会写出空表头，往返导入时该列必丢）
            var options = new ExportOptions
            {
                Columns = new List<ExportColumn> { new ExportColumn { PropertyName = nameof(DeviceDto.Code) } }
            };

            var stream = await exporter.ExportAsync(TestHelpers.DataOf(CreateDevice()), options);

            var content = TestHelpers.ReadAll(stream);
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("Code", lines[0]);
            Assert.Equal("D001", lines[1]);
        }

        [Fact]
        public async Task Export_ProgrammaticColumns_LargeOrder_SortsBeforeUnorderedColumns()
        {
            // Order ≥ 10000 的大序号仍先于 Order = 0 列（两段排序，无魔法偏移上限）
            var options = new ExportOptions<DeviceDto>()
                .Column(x => x.Code, "编码", c => c.Order = 10002)
                .Column(x => x.Name, "名称");

            var stream = await exporter.ExportAsync(TestHelpers.DataOf(CreateDevice()), options);

            var content = TestHelpers.ReadAll(stream);
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("编码,名称", lines[0]);
        }

        [Fact]
        public async Task Export_ConverterTypeFromAttribute_AppliesConversion()
        {
            var item = new ConverterDto { Code = "D009", Status = 1 };

            var stream = await exporter.ExportAsync(TestHelpers.DataOf(item), new ExportOptions());

            var content = TestHelpers.ReadAll(stream);
            Assert.Contains("D009,在线", content);
        }

        [Fact]
        public async Task Export_FormulaLikeStrings_EscapeWithQuotePrefix()
        {
            // CSV 公式注入防护：字符串列危险前缀文本加 ' 前缀（对齐 Excel/Sheets 导出行为）
            var item = new InjectionDto
            {
                Formula = "=1+1",
                Plus = "+2+3",
                Minus = "-cmd",
                At = "@SUM(A1)",
                Normal = "普通"
            };

            var stream = await exporter.ExportAsync(TestHelpers.DataOf(item), new ExportOptions());

            var content = TestHelpers.ReadAll(stream);
            Assert.Equal("公式,加号,减号,艾特,普通\r\n'=1+1,'+2+3,'-cmd,'@SUM(A1),普通\r\n", content);
        }

        [Fact]
        public async Task Export_NegativeNumber_KeepsRawText()
        {
            // 数值列负号不属公式触发（运行时类型非字符串，不经过转义分支）
            var item = new DeviceDto { Price = -1.5 };

            var stream = await exporter.ExportAsync(TestHelpers.DataOf(item), new ExportOptions());

            var content = TestHelpers.ReadAll(stream);
            Assert.DoesNotContain("'-", content);
            Assert.Contains(",-1.5,", content);
        }

        [Fact]
        public async Task Export_IncludeHeaderFalse_OmitsHeader()
        {
            var options = new ExportOptions { IncludeHeader = false };

            var stream = await exporter.ExportAsync(TestHelpers.DataOf(CreateDevice()), options);

            var content = TestHelpers.ReadAll(stream);
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Single(lines);
            Assert.StartsWith("D001,", lines[0]);
        }

        [Fact]
        public async Task Export_EmptyColumnsList_FallsBackToAttributes()
        {
            var options = new ExportOptions();

            var stream = await exporter.ExportAsync(TestHelpers.DataOf(CreateDevice()), options);

            Assert.NotNull(stream);
            var content = TestHelpers.ReadAll(stream);
            Assert.Contains("设备编码", content);
        }

        [Fact]
        public async Task Export_CustomColumns_TakePrecedenceOverAttributes()
        {
            var options = new ExportOptions
            {
                Columns = new List<ExportColumn>
                {
                    new ExportColumn { PropertyName = nameof(DeviceDto.Name), Name = "名称" }
                }
            };

            var stream = await exporter.ExportAsync(TestHelpers.DataOf(CreateDevice()), options);

            var content = TestHelpers.ReadAll(stream);
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("名称", lines[0]);
            Assert.Equal("温度传感器", lines[1]);
        }

        [Fact]
        public async Task Export_CustomColumnConverter_Applied()
        {
            var options = new ExportOptions
            {
                Columns = new List<ExportColumn>
                {
                    new ExportColumn
                    {
                        PropertyName = nameof(DeviceDto.Status),
                        Name = "状态",
                        Converter = v => (DeviceStatus)v == DeviceStatus.Online ? "在线" : "离线"
                    }
                }
            };

            var stream = await exporter.ExportAsync(TestHelpers.DataOf(CreateDevice()), options);

            var content = TestHelpers.ReadAll(stream);
            Assert.Contains("在线", content);
        }

        [Fact]
        public async Task Export_ProgrammaticColumnNonLiteralWithoutConverter_Throws()
        {
            var options = new ExportOptions
            {
                Columns = new List<ExportColumn>
                {
                    new ExportColumn { PropertyName = nameof(UnmappedTagsDto.Tags), Name = "标签" }
                }
            };

            var exception = await Assert.ThrowsAsync<ExportException>(() =>
                exporter.ExportAsync(TestHelpers.DataOf(new UnmappedTagsDto()), options));

            Assert.Contains("字面量类型", exception.Message);
        }

        [Fact]
        public async Task Export_ProgrammaticColumnNonLiteralWithConverter_ExportsConverted()
        {
            var options = new ExportOptions
            {
                Columns = new List<ExportColumn>
                {
                    new ExportColumn
                    {
                        PropertyName = nameof(UnmappedTagsDto.Tags),
                        Name = "标签",
                        Converter = v => string.Join(";", (List<string>)v)
                    }
                }
            };

            var stream = await exporter.ExportAsync(
                TestHelpers.DataOf(new UnmappedTagsDto { Tags = new List<string> { "A", "B" } }),
                options);

            var content = TestHelpers.ReadAll(stream);
            Assert.Contains("A;B", content);
        }

        [Fact]
        public async Task Export_CustomColumnFormat_Applied()
        {
            var options = new ExportOptions
            {
                Columns = new List<ExportColumn>
                {
                    new ExportColumn { PropertyName = nameof(DeviceDto.Price), Name = "价格", Format = "N2" }
                }
            };

            var stream = await exporter.ExportAsync(TestHelpers.DataOf(CreateDevice()), options);

            var content = TestHelpers.ReadAll(stream);
            Assert.Contains("1.50", content);
        }

        [Fact]
        public async Task Export_CustomColumnPropertyNotOnType_Throws()
        {
            var options = new ExportOptions
            {
                Columns = new List<ExportColumn>
                {
                    new ExportColumn { PropertyName = nameof(DeviceDto.Code), Name = "编码" },
                    new ExportColumn { PropertyName = "不存在", Name = "空列" }
                }
            };

            var exception = await Assert.ThrowsAsync<ExportException>(() =>
                exporter.ExportAsync(TestHelpers.DataOf(CreateDevice()), options));

            Assert.Contains("不存在的属性", exception.Message);
        }

        [Fact]
        public async Task Export_NoColumnsDeclared_ThrowsExportException()
        {
            var exception = await Assert.ThrowsAsync<ExportException>(() =>
                exporter.ExportAsync(TestHelpers.DataOf(new PlainDto()), new ExportOptions()));

            Assert.Contains("未声明导出列", exception.Message);
        }

        [Fact]
        public async Task Export_PropertyNameMismatchesProperty_Throws()
        {
            var options = new ExportOptions
            {
                Columns = new List<ExportColumn>
                {
                    new ExportColumn
                    {
                        PropertyName = nameof(DeviceDto.Name),
                        Property = typeof(DeviceDto).GetProperty(nameof(DeviceDto.Code))
                    }
                }
            };

            var exception = await Assert.ThrowsAsync<ExportException>(() =>
                exporter.ExportAsync(TestHelpers.DataOf(CreateDevice()), options));

            Assert.Contains("不一致", exception.Message);
        }

        [Fact]
        public async Task Export_PropertyWithoutGetter_Throws()
        {
            // 直填只写属性的 PropertyInfo 同样快速失败（不依赖回查路径的属性池过滤）
            var options = new ExportOptions
            {
                Columns = new List<ExportColumn>
                {
                    new ExportColumn { Name = "编码", Property = typeof(UnmappedWriteOnlyDto).GetProperty(nameof(UnmappedWriteOnlyDto.Code)) }
                }
            };

            var exception = await Assert.ThrowsAsync<ExportException>(() =>
                exporter.ExportAsync(TestHelpers.DataOf(new UnmappedWriteOnlyDto()), options));

            Assert.Contains("不可读", exception.Message);
        }

        [Fact]
        public async Task Export_PropertyNameReferencesWriteOnly_Throws()
        {
            var options = new ExportOptions
            {
                Columns = new List<ExportColumn> { new ExportColumn { PropertyName = nameof(UnmappedWriteOnlyDto.Code), Name = "编码" } }
            };

            var exception = await Assert.ThrowsAsync<ExportException>(() =>
                exporter.ExportAsync(TestHelpers.DataOf(new UnmappedWriteOnlyDto()), options));

            Assert.Contains("不可读", exception.Message);
        }

        [Fact]
        public async Task Export_PropertyOnly_WritesColumn()
        {
            var options = new ExportOptions
            {
                Columns = new List<ExportColumn>
                {
                    new ExportColumn { Name = "编码", Property = typeof(DeviceDto).GetProperty(nameof(DeviceDto.Code)) }
                }
            };

            var stream = await exporter.ExportAsync(TestHelpers.DataOf(CreateDevice()), options);

            var content = TestHelpers.ReadAll(stream);
            Assert.Contains("D001", content);
        }

        [Fact]
        public async Task Export_PropertyNameCaseInsensitive_Resolved()
        {
            var options = new ExportOptions
            {
                Columns = new List<ExportColumn> { new ExportColumn { PropertyName = "code", Name = "编码" } }
            };

            var stream = await exporter.ExportAsync(TestHelpers.DataOf(CreateDevice()), options);

            var content = TestHelpers.ReadAll(stream);
            Assert.Contains("D001", content);
        }

        [Fact]
        public async Task Export_ExceedsMaxRows_ThrowsExportException()
        {
            var options = new ExportOptions { MaxRows = 1 };

            var exception = await Assert.ThrowsAsync<ExportException>(() =>
                exporter.ExportAsync(TestHelpers.DataOf(CreateDevice(), CreateDevice()), options));

            Assert.Contains("上限", exception.Message);
        }

        [Fact]
        public async Task Export_ExactlyMaxRows_Succeeds()
        {
            var options = new ExportOptions { MaxRows = 2 };

            var stream = await exporter.ExportAsync(TestHelpers.DataOf(CreateDevice(), CreateDevice()), options);

            Assert.NotNull(stream);
        }

        [Fact]
        public async Task Export_MaxRowsNotPositive_Unlimited()
        {
            // MaxRows = 0 表示不限，两行数据完整导出
            var second = CreateDevice();
            second.Code = "D002";
            var options = new ExportOptions { MaxRows = 0 };

            var stream = await exporter.ExportAsync(TestHelpers.DataOf(CreateDevice(), second), options);

            var content = TestHelpers.ReadAll(stream);
            Assert.Contains("D002", content);
        }

        [Fact]
        public async Task Export_NullData_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                exporter.ExportAsync<DeviceDto>(null, new ExportOptions()));
        }

        [Fact]
        public async Task Export_DataSourceThrows_WrapsAsExportException()
        {
            var error = new InvalidOperationException("数据源故障");

            var exception = await Assert.ThrowsAsync<ExportException>(() =>
                exporter.ExportAsync(TestHelpers.DataOfThrowing<DeviceDto>(error), new ExportOptions()));

            Assert.Same(error, exception.InnerException);
            Assert.Contains("CSV 导出失败", exception.Message);
        }

        [Fact]
        public async Task Export_NullOptions_UsesDefaults()
        {
            var stream = await exporter.ExportAsync(TestHelpers.DataOf(CreateDevice()), null);

            var content = TestHelpers.ReadAll(stream);
            Assert.StartsWith("设备编码", content);
        }

        [Fact]
        public async Task Export_NullItems_Skipped()
        {
            var stream = await exporter.ExportAsync(TestHelpers.DataOf<DeviceDto>(null, CreateDevice()), new ExportOptions());

            var content = TestHelpers.ReadAll(stream);
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, lines.Length);
        }

        [Fact]
        public async Task Export_NullPropertyValue_WritesEmptyField()
        {
            var device = CreateDevice();
            device.Code = null;

            var stream = await exporter.ExportAsync(TestHelpers.DataOf(device), new ExportOptions());

            var content = TestHelpers.ReadAll(stream);
            Assert.StartsWith(",温度传感器", content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)[1]);
        }

        [Fact]
        public async Task Export_ValueWithComma_IsQuoted()
        {
            var device = CreateDevice();
            device.Name = "含,逗号";

            var stream = await exporter.ExportAsync(TestHelpers.DataOf(device), new ExportOptions());

            var content = TestHelpers.ReadAll(stream);
            Assert.Contains("\"含,逗号\"", content);
        }

        [Fact]
        public async Task Export_ReturnedStream_PositionResetToZero()
        {
            var stream = await exporter.ExportAsync(TestHelpers.DataOf(CreateDevice()), new ExportOptions());

            Assert.Equal(0, stream.Position);
        }

        [Fact]
        public async Task Export_CancelledToken_ThrowsOperationCanceledException()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                exporter.ExportAsync(TestHelpers.DataOf(CreateDevice()), new ExportOptions(), cts.Token));
        }
    }
}
