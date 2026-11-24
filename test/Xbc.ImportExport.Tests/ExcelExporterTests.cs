using ClosedXML.Excel;
using Xbc.ImportExport.Excel;

namespace Xbc.ImportExport.Tests
{
    public class ExcelExporterTests
    {
        private readonly ExcelExporter exporter = new ExcelExporter();

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

        private static async Task<XLWorkbook> GenerateWorkbookAsync(IExporter exporter, DeviceDto device, ExportOptions options)
        {
            var stream = await exporter.ExportAsync(TestHelpers.DataOf(device), options);
            return new XLWorkbook(stream);
        }

        [Fact]
        public async Task Export_AttributeColumns_WritesStyledHeaderAndTypedRows()
        {
            using (var workbook = await GenerateWorkbookAsync(exporter, CreateDevice(), new ExportOptions()))
            {
                var ws = workbook.Worksheets.First();

                Assert.Equal("设备编码", ws.Cell(1, 1).GetString());
                Assert.Equal("状态", ws.Cell(1, 8).GetString());
                Assert.True(ws.Cell(1, 1).Style.Font.Bold);

                Assert.Equal("D001", ws.Cell(2, 1).GetString());
                Assert.True(ws.Cell(2, 3).GetValue<bool>());
                Assert.Equal(10, ws.Cell(2, 4).GetValue<int>());
                Assert.Equal(1.5, ws.Cell(2, 5).GetValue<double>());
                Assert.Equal(new DateTime(2026, 1, 2), ws.Cell(2, 6).GetDateTime());
                Assert.Equal("00000000-0000-0000-0000-000000000001", ws.Cell(2, 7).GetString());
                Assert.Equal("Online", ws.Cell(2, 8).GetString());
                Assert.Equal(5, ws.Cell(2, 9).GetValue<int>());
                Assert.Equal(2, ws.LastRowUsed().RowNumber());
            }
        }

        [Fact]
        public async Task Export_WithoutHeader_AppliesColumnWidth()
        {
            // 列宽是列的属性，不随表头关闭而丢失；数据行从第 1 行开始写
            var options = new ExportOptions<DeviceDto> { IncludeHeader = false };
            options.Column(x => x.Code, "编码", c => c.Width = 30);

            using (var workbook = await GenerateWorkbookAsync(exporter, CreateDevice(), options))
            {
                var ws = workbook.Worksheets.First();

                Assert.Equal(30, ws.Column(1).Width);
                Assert.Equal("D001", ws.Cell(1, 1).GetString());
                Assert.Equal(1, ws.LastRowUsed().RowNumber());
            }
        }

        [Fact]
        public async Task Export_OverlengthString_ThrowsExportException()
        {
            // Excel 单元格 32767 字符上限：快速失败而非静默截断（无数据损坏）
            var options = new ExportOptions<DeviceDto>();
            options.Column(x => x.Name, "名称");
            var item = new DeviceDto { Code = "D001", Name = new string('A', 40000) };

            var exception = await Assert.ThrowsAsync<ExportException>(
                () => exporter.ExportAsync(TestHelpers.DataOf(item), options));
            Assert.Contains("32,767", exception.Message);
        }

        [Fact]
        public async Task Export_DefaultOptions_FallsBackToAttributeColumns()
        {
            using (var workbook = await GenerateWorkbookAsync(exporter, CreateDevice(), new ExportOptions()))
            {
                var ws = workbook.Worksheets.First();

                Assert.Equal("设备编码", ws.Cell(1, 1).GetString());
                Assert.Equal("D001", ws.Cell(2, 1).GetString());
            }
        }

        [Fact]
        public async Task Export_FormulaLikeString_WritesAsTextCell()
        {
            // xlsx 无 CSV 式公式解释面：ClosedXML 字符串赋值恒为文本单元格（锁定行为防升级回归）
            var item = new DeviceDto { Code = "=1+1" };

            using (var workbook = await GenerateWorkbookAsync(exporter, item, new ExportOptions()))
            {
                var cell = workbook.Worksheets.First().Cell(2, 1);

                Assert.Equal(XLDataType.Text, cell.DataType);
                Assert.Equal(string.Empty, cell.FormulaA1);
                Assert.Equal("=1+1", cell.GetString());
            }
        }

        [Fact]
        public async Task Export_IncludeHeaderFalse_DataStartsAtRow1()
        {
            var options = new ExportOptions { IncludeHeader = false };

            using (var workbook = await GenerateWorkbookAsync(exporter, CreateDevice(), options))
            {
                var ws = workbook.Worksheets.First();

                Assert.Equal("D001", ws.Cell(1, 1).GetString());
                Assert.False(ws.Cell(1, 1).Style.Font.Bold);
            }
        }

        [Fact]
        public async Task Export_SheetName_DefaultsToSheet1()
        {
            using (var workbook = await GenerateWorkbookAsync(exporter, CreateDevice(), new ExportOptions()))
            {
                Assert.Equal("Sheet1", workbook.Worksheets.First().Name);
            }
        }

        [Fact]
        public async Task Export_CustomSheetName_Applied()
        {
            var options = new ExportOptions { SheetName = "设备清单" };

            using (var workbook = await GenerateWorkbookAsync(exporter, CreateDevice(), options))
            {
                Assert.Equal("设备清单", workbook.Worksheets.First().Name);
            }
        }

        [Fact]
        public async Task Export_CustomColumns_ConverterFormatAndWidthApplied()
        {
            var options = new ExportOptions
            {
                Columns = new List<ExportColumn>
                {
                    new ExportColumn
                    {
                        PropertyName = nameof(DeviceDto.Status),
                        Name = "状态",
                        Width = 25,
                        Converter = v => (DeviceStatus)v == DeviceStatus.Online ? "在线" : "离线"
                    },
                    new ExportColumn { PropertyName = nameof(DeviceDto.MadeAt), Name = "生产日期", Format = "yyyy-MM-dd" }
                }
            };

            using (var workbook = await GenerateWorkbookAsync(exporter, CreateDevice(), options))
            {
                var ws = workbook.Worksheets.First();

                Assert.Equal("在线", ws.Cell(2, 1).GetString());
                Assert.Equal(25, ws.Column(1).Width);
                Assert.Equal("yyyy-MM-dd", ws.Cell(2, 2).Style.NumberFormat.Format);
                Assert.Equal(new DateTime(2026, 1, 2), ws.Cell(2, 2).GetDateTime());
            }
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
            // MaxRows 为负表示不限，两行数据完整导出（末行可读回）
            var second = CreateDevice();
            second.Code = "D002";
            var options = new ExportOptions { MaxRows = -1 };

            var stream = await exporter.ExportAsync(TestHelpers.DataOf(CreateDevice(), second), options);

            using (var workbook = new XLWorkbook(stream))
            {
                Assert.Equal("D002", workbook.Worksheets.First().LastRowUsed().Cell(1).GetString());
            }
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
            Assert.Contains("Excel 导出失败", exception.Message);
        }

        [Fact]
        public async Task Export_ExtendedTypes_WritesTypedCells()
        {
            // Excel 数字以 double 存储，long.MaxValue 超出精度，用 2^53 内安全值
            var data = new ExtendedTypesDto
            {
                L = 123456789012345L,
                M = 1.25m,
                F = 2.5f,
                FF = 2.5f,
                SB = -5,
                US = 6,
                UI = 7,
                UL = 8,
                TS = new TimeSpan(1, 2, 3),
                DTO = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(8))
            };

            var stream = await exporter.ExportAsync(TestHelpers.DataOf(data), new ExportOptions());

            using (var workbook = new XLWorkbook(stream))
            {
                var ws = workbook.Worksheets.First();
                Assert.Equal(123456789012345L, ws.Cell(2, 1).GetValue<long>());
                Assert.Equal(1.25m, ws.Cell(2, 2).GetValue<decimal>());
                Assert.Equal(2.5f, ws.Cell(2, 3).GetValue<float>());
                Assert.Equal(2.5f, ws.Cell(2, 4).GetValue<float>());
                Assert.Equal("0.00", ws.Cell(2, 4).Style.NumberFormat.Format);
                Assert.Equal(-5, ws.Cell(2, 5).GetValue<sbyte>());
                Assert.Equal(6, ws.Cell(2, 6).GetValue<ushort>());
                Assert.Equal(7u, ws.Cell(2, 7).GetValue<uint>());
                Assert.Equal(8ul, ws.Cell(2, 8).GetValue<ulong>());
                Assert.Equal(new TimeSpan(1, 2, 3), ws.Cell(2, 9).GetTimeSpan());
                Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), ws.Cell(2, 10).GetDateTime());
            }
        }

        [Fact]
        public async Task Export_NullOptions_UsesDefaults()
        {
            var stream = await exporter.ExportAsync(TestHelpers.DataOf(CreateDevice()), null);

            using (var workbook = new XLWorkbook(stream))
            {
                Assert.Equal("设备编码", workbook.Worksheets.First().Cell(1, 1).GetString());
            }
        }

        [Fact]
        public async Task Export_NullItems_Skipped()
        {
            var stream = await exporter.ExportAsync(TestHelpers.DataOf<DeviceDto>(null, CreateDevice()), new ExportOptions());

            using (var workbook = new XLWorkbook(stream))
            {
                var ws = workbook.Worksheets.First();

                Assert.Equal("D001", ws.Cell(2, 1).GetString());
                Assert.Equal(2, ws.LastRowUsed().RowNumber());
            }
        }

        [Fact]
        public async Task Export_NullPropertyValue_CellBlank()
        {
            var device = CreateDevice();
            device.Code = null;

            var stream = await exporter.ExportAsync(TestHelpers.DataOf(device), new ExportOptions());

            using (var workbook = new XLWorkbook(stream))
            {
                Assert.True(workbook.Worksheets.First().Cell(2, 1).IsEmpty());
            }
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
