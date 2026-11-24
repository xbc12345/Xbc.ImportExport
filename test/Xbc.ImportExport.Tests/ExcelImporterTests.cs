using ClosedXML.Excel;
using System.IO.Compression;
using Xbc.ImportExport.Excel;

namespace Xbc.ImportExport.Tests
{
    public class ExcelImporterTests
    {
        private readonly ExcelImporter importer = new ExcelImporter();

        private static void FillDeviceHeader(IXLWorksheet worksheet, int row = 1)
        {
            var headers = new[] { "设备编码", "设备名称", "启用", "数量", "价格", "生产日期", "编号", "状态", "备注数" };
            for (var i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(row, i + 1).Value = headers[i];
            }
        }

        private static void FillDeviceRow(IXLWorksheet worksheet, int row, string code, string enabled, int count, double price)
        {
            worksheet.Cell(row, 1).Value = code;
            worksheet.Cell(row, 2).Value = "传感器";
            worksheet.Cell(row, 3).Value = enabled;
            worksheet.Cell(row, 4).Value = count;
            worksheet.Cell(row, 5).Value = price;
            worksheet.Cell(row, 6).Value = "2026-01-02";
            worksheet.Cell(row, 7).Value = "00000000-0000-0000-0000-000000000001";
            worksheet.Cell(row, 8).Value = "online";
            worksheet.Cell(row, 9).Value = 5;
        }

        [Fact]
        public async Task Import_ConverterTypeFromAttribute_AppliesConversion()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "编码";
                ws.Cell(1, 2).Value = "状态";
                ws.Cell(2, 1).Value = "D009";
                ws.Cell(2, 2).Value = "在线";
            });

            var rows = await TestHelpers.ImportAllAsync<ImportConverterDto>(importer, stream);

            var row = Assert.Single(rows);
            Assert.Equal("D009", row.Data.Code);
            Assert.Equal(1, row.Data.Status);
            Assert.Equal("在线", row.RawValues["状态"]);
        }

        [Fact]
        public async Task Import_ProgrammaticColumns_FullBehaviorWithoutAttributes()
        {
            // 无特性实体 + 程序化列声明完整解析行为（格式/默认值/转换器），不标注特性
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "日期";
                ws.Cell(1, 2).Value = "数量";
                ws.Cell(1, 3).Value = "标签";
                ws.Cell(2, 1).Value = "20260102";
                ws.Cell(2, 2).Value = string.Empty;
                ws.Cell(2, 3).Value = "A,B";
            });
            var options = new ImportOptions
            {
                Columns = new List<ImportColumn>
                {
                    new ImportColumn { PropertyName = nameof(UnmappedFieldsDto.MadeAt), Name = "日期", Format = "yyyyMMdd" },
                    new ImportColumn { PropertyName = nameof(UnmappedFieldsDto.Count), Name = "数量", DefaultValue = 42 },
                    new ImportColumn { PropertyName = nameof(UnmappedFieldsDto.Tags), Name = "标签", Converter = v => new List<string>(v.Split(',')) }
                }
            };

            var rows = await TestHelpers.ImportAllAsync<UnmappedFieldsDto>(importer, stream, options);

            var row = Assert.Single(rows);
            Assert.Equal(new DateTime(2026, 1, 2), row.Data.MadeAt);
            Assert.Equal(42, row.Data.Count);
            Assert.Equal(new[] { "A", "B" }, row.Data.Tags);
        }

        [Fact]
        public async Task Import_BasicWorksheet_MapsColumnsToEntities()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                FillDeviceHeader(ws);
                FillDeviceRow(ws, 2, "D001", "true", 10, 1.5);
            });

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream);

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
            Assert.Equal("传感器", row.Data.Name);
            Assert.True(row.Data.Enabled);
            Assert.Equal(10, row.Data.Count);
            Assert.Equal(1.5, row.Data.Price);
            Assert.Equal(new DateTime(2026, 1, 2), row.Data.MadeAt);
            Assert.Equal(new Guid("00000000-0000-0000-0000-000000000001"), row.Data.Uid);
            Assert.Equal(DeviceStatus.Online, row.Data.Status);
            Assert.Equal(5, row.Data.Remark);
        }

        [Fact]
        public async Task Import_MultipleRows_RowNumbersAreSheetRows()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                FillDeviceHeader(ws);
                FillDeviceRow(ws, 2, "D001", "true", 10, 1.5);
                FillDeviceRow(ws, 3, "D002", "1", 20, 2.5);
            });

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream);

            Assert.Equal(2, rows.Count);
            Assert.Equal(2, rows[0].RowNumber);
            Assert.Equal(3, rows[1].RowNumber);
            Assert.Equal("D002", rows[1].Data.Code);
        }

        [Fact]
        public async Task Import_CarriesRawValues()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                FillDeviceHeader(ws);
                FillDeviceRow(ws, 2, "D001", "true", 10, 1.5);
            });

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream);

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.RawValues["设备编码"]);
            Assert.Equal("online", row.RawValues["状态"]);
        }

        [Fact]
        public async Task Import_WorksheetWithHeaderOnly_ReturnsNoRows()
        {
            var stream = TestHelpers.BuildWorksheet(ws => FillDeviceHeader(ws));

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream);

            Assert.Empty(rows);
        }

        [Fact]
        public async Task Import_StartRow_HeaderAtSpecifiedRow()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "废弃行";
                FillDeviceHeader(ws, 2);
                FillDeviceRow(ws, 3, "D001", "true", 10, 1.5);
            });
            var options = new ImportOptions { StartRow = 2 };

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream, options);

            var row = Assert.Single(rows);
            Assert.Equal(3, row.RowNumber);
            Assert.Equal("D001", row.Data.Code);
        }

        [Fact]
        public async Task Import_StartRowBeyondFile_ThrowsImportException()
        {
            // 表头行不存在（StartRow 超出数据范围），无有效表头属文件结构错误快速失败
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                FillDeviceHeader(ws);
                FillDeviceRow(ws, 2, "D001", "true", 10, 1.5);
            });
            var options = new ImportOptions { StartRow = 100 };

            var exception = await Assert.ThrowsAsync<ImportException>(
                () => TestHelpers.ImportAllAsync<DeviceDto>(importer, stream, options));

            Assert.Contains("无有效表头", exception.Message);
        }

        [Fact]
        public async Task Import_EmptyWorksheet_ThrowsImportException()
        {
            // 工作表存在但无任何内容，无有效表头属文件结构错误快速失败
            var stream = TestHelpers.BuildWorksheet(ws => { });

            var exception = await Assert.ThrowsAsync<ImportException>(
                () => TestHelpers.ImportAllAsync<DeviceDto>(importer, stream));

            Assert.Contains("无有效表头", exception.Message);
        }

        [Fact]
        public async Task Import_WorkbookWithoutWorksheets_ThrowsImportException()
        {
            // 零工作表工作簿：连表头行的载体都不存在，属文件结构错误快速失败（对齐表头先决契约）
            // ClosedXML 无法保存零工作表工作簿（SaveAs 校验拒绝），手工构造最小零表 xlsx 包验证
            var stream = BuildEmptyWorkbook();

            var exception = await Assert.ThrowsAsync<ImportException>(
                () => TestHelpers.ImportAllAsync<DeviceDto>(importer, stream));

            Assert.Contains("不包含任何工作表", exception.Message);
        }

        /// <summary>构造只含 workbook.xml（sheets 为空）的最小 xlsx 包。</summary>
        private static MemoryStream BuildEmptyWorkbook()
        {
            var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                WriteEntry(archive, "[Content_Types].xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                    "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                    "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                    "</Types>");
                WriteEntry(archive, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "</Relationships>");
                WriteEntry(archive, "xl/workbook.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
                    "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets/></workbook>");
            }

            stream.Position = 0;
            return stream;
        }

        private static void WriteEntry(ZipArchive archive, string path, string content)
        {
            var entry = archive.CreateEntry(path);
            using (var writer = new StreamWriter(entry.Open()))
            {
                writer.Write(content);
            }
        }

        [Fact]
        public async Task Import_EmptyStream_ThrowsImportException()
        {
            // 0 字节流不是合法 xlsx 包，工作簿构造即抛并包装为 ImportException
            using var stream = new MemoryStream();
            var exception = await Assert.ThrowsAsync<ImportException>(
                () => TestHelpers.ImportAllAsync<DeviceDto>(importer, stream));
            Assert.Contains("工作簿打开失败", exception.Message);
        }

        [Fact]
        public async Task Import_DuplicateHeaderNames_ThrowsImportException()
        {
            // 重复列名无法建立唯一列映射，快速失败而非静默取最后一列（与 CSV 侧 BuildHeaderMap 同一契约）
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "编码";
                ws.Cell(1, 2).Value = "编码";
                ws.Cell(2, 1).Value = "D001";
                ws.Cell(2, 2).Value = "D002";
            });
            var options = new ImportOptions<DeviceDto>().Column(x => x.Code, "编码");

            var act = () => TestHelpers.ImportAllAsync<DeviceDto>(importer, stream, options);

            var ex = await Assert.ThrowsAsync<ImportException>(act);
            Assert.Contains("编码", ex.Message);
            Assert.Contains("重复", ex.Message);
        }

        [Fact]
        public async Task Import_TimeSpanCellWithFormat_ReadByDeclaredFormat()
        {
            // TimeSpan 类型化单元格在列声明 Format 时按该模式取文本（与 DateTime 分支对称），避免默认文本与解析格式断裂
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "TS";
                ws.Cell(2, 1).Value = new TimeSpan(1, 2, 3, 4);
            });
            var options = new ImportOptions<TypesDto>().Column(x => x.TS, "TS", c => c.Format = @"hh\:mm\:ss");

            var rows = await TestHelpers.ImportAllAsync<TypesDto>(importer, stream, options);

            var row = Assert.Single(rows);
            Assert.Equal(new TimeSpan(2, 3, 4), row.Data.TS);
        }

        [Fact]
        public async Task Import_SkipEmptyRows_SkipsBlankRows()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                FillDeviceHeader(ws);
                FillDeviceRow(ws, 2, "D001", "true", 10, 1.5);
                FillDeviceRow(ws, 4, "D002", "1", 20, 2.5);
            });

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream);

            Assert.Equal(2, rows.Count);
            Assert.Equal(2, rows[0].RowNumber);
            Assert.Equal(4, rows[1].RowNumber);
        }

        [Fact]
        public async Task Import_WhitespaceRow_TreatedAsEmptyRow()
        {
            // 整行仅空格字符的行按空行处理（空白判定对齐 CSV 侧 IsNullOrWhiteSpace），SkipEmptyRows 时跳过
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                FillDeviceHeader(ws);
                FillDeviceRow(ws, 2, "D001", "true", 10, 1.5);
                ws.Cell(3, 1).Value = " ";
                FillDeviceRow(ws, 4, "D002", "1", 20, 2.5);
            });

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream);

            Assert.Equal(2, rows.Count);
            Assert.Equal(2, rows[0].RowNumber);
            Assert.Equal(4, rows[1].RowNumber);
        }

        [Fact]
        public async Task Import_KeepEmptyRows_YieldsRowWithDefaults()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                FillDeviceHeader(ws);
                for (var i = 1; i <= 9; i++)
                {
                    ws.Cell(2, i).Value = string.Empty;
                }
                // 空字符串赋值不标记行已使用，需在映射列之外放标记值让 LastRowUsed 覆盖行 2
                ws.Cell(2, 10).Value = "x";
            });
            var options = new ImportOptions { SkipEmptyRows = false };

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream, options);

            var row = Assert.Single(rows);
            Assert.Null(row.Data.Code);
            Assert.Equal(0, row.Data.Count);
        }

        [Fact]
        public async Task Import_LongAndDecimalFields_Converted()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "L";
                ws.Cell(1, 2).Value = "M";
                ws.Cell(2, 1).Value = "9223372036854775807";
                ws.Cell(2, 2).Value = "1.25";
            });

            var rows = await TestHelpers.ImportAllAsync<TypesDto>(importer, stream);

            var row = Assert.Single(rows);
            Assert.Equal(long.MaxValue, row.Data.L);
            Assert.Equal(1.25m, row.Data.M);
        }

        [Fact]
        public async Task Import_UnsignedAndSignedSmallIntegerFields_Converted()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "SB";
                ws.Cell(1, 2).Value = "US";
                ws.Cell(1, 3).Value = "UI";
                ws.Cell(1, 4).Value = "UL";
                ws.Cell(2, 1).Value = "-128";
                ws.Cell(2, 2).Value = "65535";
                ws.Cell(2, 3).Value = "4294967295";
                ws.Cell(2, 4).Value = "18446744073709551615";
            });

            var rows = await TestHelpers.ImportAllAsync<TypesDto>(importer, stream);

            var row = Assert.Single(rows);
            Assert.Equal(sbyte.MinValue, row.Data.SB);
            Assert.Equal(ushort.MaxValue, row.Data.US);
            Assert.Equal(uint.MaxValue, row.Data.UI);
            Assert.Equal(ulong.MaxValue, row.Data.UL);
        }

        [Fact]
        public async Task Import_NarrowingOverflow_CollectsError()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "US";
                ws.Cell(1, 2).Value = "SB";
                ws.Cell(2, 1).Value = "65536";
                ws.Cell(2, 2).Value = "300";
            });

            var options = new ImportOptions { ErrorMode = ImportErrorMode.Collect };
            var rows = await TestHelpers.ImportAllAsync<TypesDto>(importer, stream, options);

            var row = Assert.Single(rows);
            Assert.Equal(0, row.Data.US);
            Assert.Equal(0, row.Data.SB);
            Assert.Equal(2, row.Errors.Count);
        }

        [Fact]
        public async Task Import_DateTimeOffsetField_Parsed()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "DTO";
                ws.Cell(2, 1).Value = "2024-06-15T10:30:00+08:00";
            });

            var rows = await TestHelpers.ImportAllAsync<TypesDto>(importer, stream);

            var row = Assert.Single(rows);
            Assert.Equal(new DateTimeOffset(2024, 6, 15, 10, 30, 0, TimeSpan.FromHours(8)), row.Data.DTO);
        }

        [Fact]
        public async Task Import_TimeSpanField_Parsed()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "TS";
                ws.Cell(2, 1).Value = "1.02:03:04";
            });

            var rows = await TestHelpers.ImportAllAsync<TypesDto>(importer, stream);

            var row = Assert.Single(rows);
            Assert.Equal(new TimeSpan(1, 2, 3, 4), row.Data.TS);
        }

        [Fact]
        public async Task Import_NonLiteralFieldWithConverter_Converted()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "标签";
                ws.Cell(2, 1).Value = "A,B,C";
            });

            var rows = await TestHelpers.ImportAllAsync<StringListDto>(importer, stream);

            var row = Assert.Single(rows);
            Assert.Equal(new List<string> { "A", "B", "C" }, row.Data.Tags);
        }

        [Fact]
        public async Task Import_UnmappedColumnHasValue_NotTreatedAsEmptyRow()
        {
            // 映射列全空但未映射列有值：整行非空，产出默认值行而非跳过
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "编码";
                ws.Cell(1, 2).Value = "备注";
                ws.Cell(2, 1).Value = "";
                ws.Cell(2, 2).Value = "备注内容";
            });
            var options = new ImportOptions
            {
                Columns = new List<ImportColumn> { new ImportColumn { PropertyName = nameof(PlainDto.Code), Name = "编码" } }
            };

            var rows = await TestHelpers.ImportAllAsync<PlainDto>(importer, stream, options);

            var row = Assert.Single(rows);
            Assert.Null(row.Data.Code);
            Assert.Equal(string.Empty, row.RawValues["编码"]);
        }

        [Fact]
        public async Task Import_ProgrammaticColumnReferencesUnknownProperty_ThrowsImportException()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "编码";
                ws.Cell(1, 2).Value = "名称";
                ws.Cell(2, 1).Value = "D001";
                ws.Cell(2, 2).Value = "传感器";
            });
            var options = new ImportOptions
            {
                Columns = new List<ImportColumn> { new ImportColumn { PropertyName = "NotAProperty", Name = "编码" } }
            };

            var exception = await Assert.ThrowsAsync<ImportException>(
                async () => await TestHelpers.ImportAllAsync<PlainDto>(importer, stream, options));

            Assert.Contains("NotAProperty", exception.Message);
        }

        [Fact]
        public async Task Import_EntityWithIndexer_BindsNormalProperties()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "编码";
                ws.Cell(2, 1).Value = "D001";
            });
            var options = new ImportOptions
            {
                Columns = new List<ImportColumn> { new ImportColumn { PropertyName = nameof(IndexedDto.Code), Name = "编码" } }
            };

            var rows = await TestHelpers.ImportAllAsync<IndexedDto>(importer, stream, options);

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
        }

        [Fact]
        public async Task Import_ProgrammaticColumnReferencesIndexer_ThrowsImportException()
        {
            // 索引器（this[int]）不参与导入绑定，程序化列引用索引器名应 fail-fast
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "编码";
                ws.Cell(2, 1).Value = "D001";
            });
            var options = new ImportOptions
            {
                Columns = new List<ImportColumn> { new ImportColumn { PropertyName = "Item", Name = "编码" } }
            };

            var exception = await Assert.ThrowsAsync<ImportException>(
                async () => await TestHelpers.ImportAllAsync<IndexedDto>(importer, stream, options));

            Assert.Contains("Item", exception.Message);
        }

        [Fact]
        public async Task Import_DisposedStream_ThrowsImportException()
        {
            // 流不可用属调用方使用错误（区别于数据行问题的 ImportDataException）
            var stream = TestHelpers.BuildWorksheet(ws => { ws.Cell(1, 1).Value = "编码"; });
            stream.Dispose();

            var exception = await Assert.ThrowsAsync<ImportException>(
                async () => await TestHelpers.ImportAllAsync<PlainDto>(importer, stream));

            Assert.Contains("Excel 工作簿打开失败", exception.Message);
            Assert.IsNotType<ImportDataException>(exception);
        }

        [Fact]
        public async Task Import_BooleanValue_AcceptsTrueAndFalseSynonyms()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "启用";
                ws.Cell(2, 1).Value = "yes";
                ws.Cell(3, 1).Value = "no";
            });

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream);

            Assert.Equal(2, rows.Count);
            Assert.True(rows[0].Data.Enabled);
            Assert.False(rows[1].Data.Enabled);
        }

        [Fact]
        public async Task Import_UnknownEnumValue_LenientMode_FallsBackSilently()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "状态";
                ws.Cell(2, 1).Value = "xyz";
            });

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream);

            var row = Assert.Single(rows);
            Assert.Equal(DeviceStatus.Online, row.Data.Status);
            Assert.Empty(row.Errors);
        }

        [Fact]
        public async Task Import_UnknownEnumValue_StrictMode_ThrowsImportDataException()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "状态";
                ws.Cell(2, 1).Value = "xyz";
            });
            var options = new ImportOptions { ErrorMode = ImportErrorMode.Strict };

            var exception = await Assert.ThrowsAsync<ImportDataException>(
                async () => await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream, options));

            Assert.Equal(2, exception.RowNumber);
            Assert.Equal("状态", exception.ColumnName);
            Assert.Contains("xyz", exception.Message);
        }

        [Fact]
        public async Task Import_UnknownEnumValue_CollectMode_RecordsErrorAndContinues()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "状态";
                ws.Cell(2, 1).Value = "xyz";
                ws.Cell(3, 1).Value = "online";
            });
            var options = new ImportOptions { ErrorMode = ImportErrorMode.Collect };

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream, options);

            Assert.Equal(2, rows.Count);
            var error = Assert.Single(rows[0].Errors);
            Assert.Contains("状态", error);
            Assert.Contains("xyz", error);
            Assert.Equal(DeviceStatus.Online, rows[0].Data.Status);
            Assert.Empty(rows[1].Errors);
            Assert.Equal(DeviceStatus.Online, rows[1].Data.Status);
        }

        [Fact]
        public async Task Import_InvalidNumber_CollectMode_RecordsError()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "数量";
                ws.Cell(2, 1).Value = "abc";
            });
            var options = new ImportOptions { ErrorMode = ImportErrorMode.Collect };

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream, options);

            var row = Assert.Single(rows);
            var error = Assert.Single(row.Errors);
            Assert.Contains("数量", error);
            Assert.Equal(0, row.Data.Count);
        }

        [Fact]
        public async Task Import_EmptyValue_UsesDeclaredDefaultValue()
        {
            // Enabled 声明 DefaultValue = true，空单元格应得到 true 而非类型默认 false
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "启用";
                ws.Cell(1, 2).Value = "状态";
                ws.Cell(2, 2).Value = "online";
            });

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream);

            var row = Assert.Single(rows);
            Assert.True(row.Data.Enabled);
            Assert.Equal(DeviceStatus.Online, row.Data.Status);
        }

        [Fact]
        public async Task Import_EmptyValue_ObjectDefaults_ApplyDirectly()
        {
            // 类型兼容字面量直用（int、枚举），字符串声明仍按解析路径；未映射列占位避免整行被识别为空行
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "数量";
                ws.Cell(1, 2).Value = "状态";
                ws.Cell(1, 3).Value = "备注";
                ws.Cell(2, 4).Value = "x";
            });

            var rows = await TestHelpers.ImportAllAsync<ObjectDefaultDto>(importer, stream);

            var row = Assert.Single(rows);
            Assert.Equal(100, row.Data.Count);
            Assert.Equal(DeviceStatus.Online, row.Data.Status);
            Assert.Equal("文本", row.Data.Note);
        }

        [Fact]
        public async Task Import_ProgrammaticColumns_MapsByPropertyName()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "编码";
                ws.Cell(1, 2).Value = "名称";
                ws.Cell(2, 1).Value = "D001";
                ws.Cell(2, 2).Value = "传感器";
            });
            var options = new ImportOptions
            {
                Columns = new List<ImportColumn>
                {
                    new ImportColumn { PropertyName = nameof(PlainDto.Code), Name = "编码" },
                    new ImportColumn { PropertyName = nameof(PlainDto.Name), Name = "名称" }
                }
            };

            var rows = await TestHelpers.ImportAllAsync<PlainDto>(importer, stream, options);

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
            Assert.Equal("传感器", row.Data.Name);
        }

        [Fact]
        public async Task Import_ExtraColumn_Ignored()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                FillDeviceHeader(ws);
                ws.Cell(1, 10).Value = "多余列";
                FillDeviceRow(ws, 2, "D001", "true", 10, 1.5);
                ws.Cell(2, 10).Value = "多余值";
            });

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream);

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
            Assert.False(row.RawValues.ContainsKey("多余列"));
        }

        [Fact]
        public async Task Import_AttributeColumnMissingInHeader_Skipped()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "设备编码";
                ws.Cell(1, 2).Value = "设备名称";
                ws.Cell(2, 1).Value = "D001";
                ws.Cell(2, 2).Value = "传感器";
            });

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream);

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
            Assert.Equal(0, row.Data.Count);
            Assert.False(row.RawValues.ContainsKey("数量"));
        }

        [Fact]
        public async Task Import_BooleanForms_AcceptsTrueOneAndChinese()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "设备编码";
                ws.Cell(1, 2).Value = "启用";
                for (var i = 0; i < 4; i++)
                {
                    ws.Cell(i + 2, 1).Value = "D00" + (i + 1);
                }
                ws.Cell(2, 2).Value = "是";
                ws.Cell(3, 2).Value = "1";
                ws.Cell(4, 2).Value = "true";
                ws.Cell(5, 2).Value = "别的";
            });

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream);

            Assert.True(rows[0].Data.Enabled);
            Assert.True(rows[1].Data.Enabled);
            Assert.True(rows[2].Data.Enabled);
            Assert.False(rows[3].Data.Enabled);
        }

        [Fact]
        public async Task Import_InvalidValues_SilentlyDefault()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "设备编码";
                ws.Cell(1, 2).Value = "数量";
                ws.Cell(1, 3).Value = "编号";
                ws.Cell(1, 4).Value = "状态";
                ws.Cell(2, 1).Value = "D001";
                ws.Cell(2, 2).Value = "abc";
                ws.Cell(2, 3).Value = "not-a-guid";
                ws.Cell(2, 4).Value = "不存在";
            });

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream);

            var row = Assert.Single(rows);
            Assert.Equal(0, row.Data.Count);
            Assert.Equal(Guid.Empty, row.Data.Uid);
            Assert.Equal(DeviceStatus.Online, row.Data.Status);
        }

        [Fact]
        public async Task Import_EmptyFields_UseTypeDefaults()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                ws.Cell(1, 1).Value = "设备编码";
                ws.Cell(1, 2).Value = "数量";
                ws.Cell(1, 3).Value = "备注数";
                ws.Cell(2, 1).Value = "D001";
            });

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream);

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
            Assert.Equal(0, row.Data.Count);
            Assert.Null(row.Data.Remark);
        }

        [Fact]
        public async Task Import_NullStream_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                TestHelpers.ImportAllAsync<DeviceDto>(importer, null));
        }

        [Fact]
        public async Task Import_NullOptions_UsesDefaults()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                FillDeviceHeader(ws);
                FillDeviceRow(ws, 2, "D001", "true", 10, 1.5);
            });

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, stream, null);

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
        }

        [Fact]
        public async Task Import_CancelledToken_ThrowsOperationCanceledException()
        {
            var stream = TestHelpers.BuildWorksheet(ws =>
            {
                FillDeviceHeader(ws);
                FillDeviceRow(ws, 2, "D001", "true", 10, 1.5);
            });
            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                TestHelpers.ImportAllAsync<DeviceDto>(importer, stream, null, cts.Token));
        }
    }
}
