using Xbc.ImportExport.Csv;

namespace Xbc.ImportExport.Tests
{
    public class CsvImporterTests
    {
        private readonly CsvImporter importer = new CsvImporter();

        private const string DeviceHeader = "设备编码,设备名称,启用,数量,价格,生产日期,编号,状态,备注数";

        [Fact]
        public async Task Import_BasicCsv_MapsColumnsToEntities()
        {
            var content = DeviceHeader + "\nD001,温度传感器,true,10,1.5,2026-01-02,00000000-0000-0000-0000-000000000001,online,5";

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
            Assert.Equal("温度传感器", row.Data.Name);
            Assert.True(row.Data.Enabled);
            Assert.Equal(10, row.Data.Count);
            Assert.Equal(1.5, row.Data.Price);
            Assert.Equal(new DateTime(2026, 1, 2), row.Data.MadeAt);
            Assert.Equal(new Guid("00000000-0000-0000-0000-000000000001"), row.Data.Uid);
            Assert.Equal(DeviceStatus.Online, row.Data.Status);
            Assert.Equal(5, row.Data.Remark);
        }

        [Fact]
        public async Task Import_BooleanValue_AcceptsTrueAndFalseSynonyms()
        {
            var content = "启用\nyes\nno";

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content));

            Assert.Equal(2, rows.Count);
            Assert.True(rows[0].Data.Enabled);
            Assert.False(rows[1].Data.Enabled);
        }

        [Fact]
        public async Task Import_UnknownEnumValue_LenientMode_FallsBackSilently()
        {
            var content = "状态\nxyz";

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal(DeviceStatus.Online, row.Data.Status);
            Assert.Empty(row.Errors);
        }

        [Fact]
        public async Task Import_UnknownEnumValue_StrictMode_ThrowsImportDataException()
        {
            var content = "状态\nxyz";
            var options = new ImportOptions { ErrorMode = ImportErrorMode.Strict };

            var exception = await Assert.ThrowsAsync<ImportDataException>(
                async () => await TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content), options));

            Assert.Equal(2, exception.RowNumber);
            Assert.Equal("状态", exception.ColumnName);
            Assert.Contains("xyz", exception.Message);
        }

        [Fact]
        public async Task Import_UnknownEnumValue_CollectMode_RecordsErrorAndContinues()
        {
            var content = "状态\nxyz\nonline";
            var options = new ImportOptions { ErrorMode = ImportErrorMode.Collect };

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content), options);

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
            var content = "数量\nabc";
            var options = new ImportOptions { ErrorMode = ImportErrorMode.Collect };

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content), options);

            var row = Assert.Single(rows);
            var error = Assert.Single(row.Errors);
            Assert.Contains("数量", error);
            Assert.Equal(0, row.Data.Count);
        }

        [Fact]
        public async Task Import_EmptyValue_UsesDeclaredDefaultValue()
        {
            // Enabled 声明 DefaultValue = true，空单元格应得到 true 而非类型默认 false
            var content = "启用,状态\n,online";

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.True(row.Data.Enabled);
            Assert.Equal(DeviceStatus.Online, row.Data.Status);
        }

        [Fact]
        public async Task Import_EmptyValue_ObjectDefaults_ApplyDirectly()
        {
            // 类型兼容字面量直用（int、枚举），字符串声明仍按解析路径；冗余列占位避免整行被识别为空行
            var content = "数量,状态,备注,冗余\n,,,x";

            var rows = await TestHelpers.ImportAllAsync<ObjectDefaultDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal(100, row.Data.Count);
            Assert.Equal(DeviceStatus.Online, row.Data.Status);
            Assert.Equal("文本", row.Data.Note);
        }

        [Fact]
        public async Task Import_ConverterTypeFromAttribute_AppliesConversion()
        {
            var content = "编码,状态\nD009,在线";

            var rows = await TestHelpers.ImportAllAsync<ImportConverterDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal("D009", row.Data.Code);
            Assert.Equal(1, row.Data.Status);
            Assert.Equal("在线", row.RawValues["状态"]);
        }

        [Fact]
        public async Task Import_ConverterTypeEmptyValue_FallsBackToDefault()
        {
            var content = "编码,状态\nD009,";

            var rows = await TestHelpers.ImportAllAsync<ImportConverterDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal(0, row.Data.Status);
        }

        [Fact]
        public async Task Import_BasicCsv_CarriesRowNumberAndRawValues()
        {
            var content = DeviceHeader + "\nD001,传感器,true,1,1,2026-01-02,00000000-0000-0000-0000-000000000001,online,1";

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal(2, row.RowNumber);
            Assert.Equal("D001", row.RawValues["设备编码"]);
            Assert.Equal("online", row.RawValues["状态"]);
        }

        [Fact]
        public async Task Import_MultipleRows_NumbersSequentially()
        {
            var line = "D001,传感器,true,1,1,2026-01-02,00000000-0000-0000-0000-000000000001,online,1";
            var content = DeviceHeader + "\n" + line + "\n" + line;

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content));

            Assert.Equal(2, rows.Count);
            Assert.Equal(2, rows[0].RowNumber);
            Assert.Equal(3, rows[1].RowNumber);
        }

        [Fact]
        public async Task Import_EmptyStream_ThrowsImportException()
        {
            // 空文件无表头可解析，属文件结构错误快速失败
            var exception = await Assert.ThrowsAsync<ImportException>(
                () => TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(string.Empty)));

            Assert.Contains("无有效表头", exception.Message);
        }

        [Fact]
        public async Task Import_HeaderOnly_ReturnsNoRows()
        {
            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(DeviceHeader));

            Assert.Empty(rows);
        }

        [Fact]
        public async Task Import_StartRow_SkipsRowsBeforeHeader()
        {
            var content = "废弃行1\n废弃行2\n" + DeviceHeader + "\nD001,传感器,true,1,1,2026-01-02,00000000-0000-0000-0000-000000000001,online,1";
            var options = new ImportOptions { StartRow = 3 };

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content), options);

            var row = Assert.Single(rows);
            Assert.Equal(4, row.RowNumber);
            Assert.Equal("D001", row.Data.Code);
        }

        [Fact]
        public async Task Import_StartRowBeyondFile_ThrowsImportException()
        {
            // 文件行数不足起始行，表头行读不到，属文件结构错误快速失败
            var content = DeviceHeader + "\nD001,传感器,true,1,1,2026-01-02,00000000-0000-0000-0000-000000000001,online,1";
            var options = new ImportOptions { StartRow = 10 };

            var exception = await Assert.ThrowsAsync<ImportException>(
                () => TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content), options));

            Assert.Contains("无有效表头", exception.Message);
        }

        [Fact]
        public async Task Import_BlankHeaderRow_ThrowsImportException()
        {
            // 表头行存在但列名全为空白，同样无有效表头
            var content = "  ,  \nD001,x";

            var exception = await Assert.ThrowsAsync<ImportException>(
                () => TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content)));

            Assert.Contains("无有效表头", exception.Message);
        }

        [Fact]
        public async Task Import_DuplicateHeaderNames_ThrowsImportException()
        {
            // 重复列名无法建立唯一列映射，快速失败而非静默取最后一列（与 Excel 侧 BuildHeaderMap 同一契约）
            var content = "编码,编码\nD001,D002";
            var options = new ImportOptions<DeviceDto>().Column(x => x.Code, "编码");

            var act = () => TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content), options);

            var ex = await Assert.ThrowsAsync<ImportException>(act);
            Assert.Contains("编码", ex.Message);
            Assert.Contains("重复", ex.Message);
        }

        [Fact]
        public async Task Import_TruncatedRow_MissingColumnsUseDefaults()
        {
            // 截断行（字段数少于表头列数）不中断导入，缺失列按空值走声明默认值路径
            var content = "设备编码,启用\r\nD001";

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
            Assert.True(row.Data.Enabled); // 缺失列应用 [ImportColumn] DefaultValue = true
        }

        [Fact]
        public async Task Import_SkipEmptyRows_SkipsBlankLines()
        {
            var line = "D001,传感器,true,1,1,2026-01-02,00000000-0000-0000-0000-000000000001,online,1";
            var content = DeviceHeader + "\n" + line + "\n,,,,,,,,";

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
        }

        [Fact]
        public async Task Import_UnmappedColumnHasValue_NotTreatedAsEmptyRow()
        {
            // 映射列全空但未映射列有值：整行非空，产出默认值行而非跳过
            var content = "编码,备注\n,备注内容";
            var options = new ImportOptions
            {
                Columns = new List<ImportColumn> { new ImportColumn { PropertyName = nameof(PlainDto.Code), Name = "编码" } }
            };

            var rows = await TestHelpers.ImportAllAsync<PlainDto>(importer, TestHelpers.ToStream(content), options);

            var row = Assert.Single(rows);
            Assert.Null(row.Data.Code);
            Assert.Equal(string.Empty, row.RawValues["编码"]);
        }

        [Fact]
        public async Task Import_KeepEmptyRows_PhysicalBlankLineYieldsRowWithDefaults()
        {
            var content = "编码\nD001\n\nD002";
            var options = new ImportOptions
            {
                SkipEmptyRows = false,
                Columns = new List<ImportColumn> { new ImportColumn { PropertyName = nameof(PlainDto.Code), Name = "编码" } }
            };

            var rows = await TestHelpers.ImportAllAsync<PlainDto>(importer, TestHelpers.ToStream(content), options);

            Assert.Equal(3, rows.Count);
            Assert.Equal("D001", rows[0].Data.Code);
            Assert.Null(rows[1].Data.Code);
            Assert.Equal(string.Empty, rows[1].RawValues["编码"]);
            Assert.Equal("D002", rows[2].Data.Code);
        }

        [Fact]
        public async Task Import_KeepEmptyRows_YieldsRowWithDefaults()
        {
            var content = DeviceHeader + "\n,,,,,,,,";

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content), new ImportOptions { SkipEmptyRows = false });

            var row = Assert.Single(rows);
            Assert.Null(row.Data.Code);
            Assert.Equal(string.Empty, row.RawValues["设备编码"]);
        }

        [Fact]
        public async Task Import_EntityWithIndexer_BindsNormalProperties()
        {
            var content = "编码\nD001";
            var options = new ImportOptions
            {
                Columns = new List<ImportColumn> { new ImportColumn { PropertyName = nameof(IndexedDto.Code), Name = "编码" } }
            };

            var rows = await TestHelpers.ImportAllAsync<IndexedDto>(importer, TestHelpers.ToStream(content), options);

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
        }

        [Fact]
        public async Task Import_ProgrammaticColumnReferencesIndexer_ThrowsImportException()
        {
            // 索引器（this[int]）不参与导入绑定，程序化列引用索引器名应 fail-fast
            var content = "编码\nD001";
            var options = new ImportOptions
            {
                Columns = new List<ImportColumn> { new ImportColumn { PropertyName = "Item", Name = "编码" } }
            };

            var exception = await Assert.ThrowsAsync<ImportException>(
                async () => await TestHelpers.ImportAllAsync<IndexedDto>(importer, TestHelpers.ToStream(content), options));

            Assert.Contains("Item", exception.Message);
        }

        [Fact]
        public async Task Import_DisposedStream_ThrowsImportException()
        {
            // 流不可用属调用方使用错误（区别于数据行问题的 ImportDataException）
            var stream = TestHelpers.ToStream("编码\nD001");
            stream.Dispose();

            var exception = await Assert.ThrowsAsync<ImportException>(
                async () => await TestHelpers.ImportAllAsync<PlainDto>(importer, stream));

            Assert.Contains("CSV 流打开失败", exception.Message);
            Assert.IsNotType<ImportDataException>(exception);
        }

        [Fact]
        public async Task Import_ProgrammaticColumns_MapsByPropertyName()
        {
            var content = "编码,名称\nD001,传感器";
            var options = new ImportOptions
            {
                Columns = new List<ImportColumn>
                {
                    new ImportColumn { PropertyName = nameof(PlainDto.Code), Name = "编码" },
                    new ImportColumn { PropertyName = nameof(PlainDto.Name), Name = "名称" }
                }
            };

            var rows = await TestHelpers.ImportAllAsync<PlainDto>(importer, TestHelpers.ToStream(content), options);

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
            Assert.Equal("传感器", row.Data.Name);
            Assert.Equal("D001", row.RawValues["编码"]);
        }

        [Fact]
        public async Task Import_ProgrammaticColumn_PropertyNameMismatchesProperty_ThrowsImportException()
        {
            var content = "编码\nD001";
            var options = new ImportOptions
            {
                Columns = new List<ImportColumn>
                {
                    new ImportColumn
                    {
                        Name = "编码",
                        PropertyName = nameof(PlainDto.Name),
                        Property = typeof(PlainDto).GetProperty(nameof(PlainDto.Code))
                    }
                }
            };

            var exception = await Assert.ThrowsAsync<ImportException>(
                async () => await TestHelpers.ImportAllAsync<PlainDto>(importer, TestHelpers.ToStream(content), options));

            Assert.Contains("不一致", exception.Message);
        }

        [Fact]
        public async Task Import_ProgrammaticColumn_PropertyOnly_BindsByProperty()
        {
            var content = "编码\nD001";
            var options = new ImportOptions
            {
                Columns = new List<ImportColumn>
                {
                    new ImportColumn { Name = "编码", Property = typeof(PlainDto).GetProperty(nameof(PlainDto.Code)) }
                }
            };

            var rows = await TestHelpers.ImportAllAsync<PlainDto>(importer, TestHelpers.ToStream(content), options);

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
        }

        [Fact]
        public async Task Import_ProgrammaticColumn_EmptyName_FallsBackToPropertyName()
        {
            // 直填列名留空回退属性名（与特性声明、强类型选项路径统一；空名列无法匹配表头会静默丢列）
            var content = "Code,名称\nD001,传感器";
            var options = new ImportOptions
            {
                Columns = new List<ImportColumn>
                {
                    new ImportColumn { PropertyName = nameof(PlainDto.Code) },
                    new ImportColumn { PropertyName = nameof(PlainDto.Name), Name = "名称" }
                }
            };

            var rows = await TestHelpers.ImportAllAsync<PlainDto>(importer, TestHelpers.ToStream(content), options);

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
            Assert.Equal("传感器", row.Data.Name);
            Assert.Equal("D001", row.RawValues["Code"]);
        }

        [Fact]
        public async Task Import_ProgrammaticColumn_ColumnNotInHeader_Ignored()
        {
            var content = "编码\nD001";
            var options = new ImportOptions
            {
                Columns = new List<ImportColumn>
                {
                    new ImportColumn { PropertyName = nameof(PlainDto.Code), Name = "编码" },
                    new ImportColumn { PropertyName = nameof(PlainDto.Name), Name = "不存在" }
                }
            };

            var rows = await TestHelpers.ImportAllAsync<PlainDto>(importer, TestHelpers.ToStream(content), options);

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
            Assert.False(row.RawValues.ContainsKey("不存在"));
        }

        [Fact]
        public async Task Import_TypedMapConfigure_FullBehaviorWithoutAttributes()
        {
            // 无特性实体 + 强类型 Map 声明完整解析行为（格式/默认值/转换器），不标注特性
            var content = "日期,数量,标签\n20260102,,\"A,B\"";
            var options = new ImportOptions<UnmappedFieldsDto>()
                .Column(x => x.MadeAt, "日期", c => c.Format = "yyyyMMdd")
                .Column(x => x.Count, "数量", c => c.DefaultValue = 42)
                .Column(x => x.Tags, "标签", c => c.Converter = v => new List<string>(v.Split(',')));

            var rows = await TestHelpers.ImportAllAsync<UnmappedFieldsDto>(importer, TestHelpers.ToStream(content), options);

            var row = Assert.Single(rows);
            Assert.Equal(new DateTime(2026, 1, 2), row.Data.MadeAt);
            Assert.Equal(42, row.Data.Count);
            Assert.Equal(new[] { "A", "B" }, row.Data.Tags);
        }

        [Fact]
        public async Task Import_ProgrammaticColumns_ReplacesAttributeColumns()
        {
            // 程序化列生效时完全替代特性声明列（Name 不导入）
            var content = "编码,名称\nD001,传感器";
            var options = new ImportOptions
            {
                Columns = new List<ImportColumn> { new ImportColumn { PropertyName = nameof(DeviceDto.Code), Name = "编码" } }
            };

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content), options);

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
            Assert.Null(row.Data.Name);
        }

        [Fact]
        public async Task Import_ProgrammaticColumn_NonLiteralWithoutConverter_Throws()
        {
            var content = "标签\nA,B";
            var options = new ImportOptions
            {
                Columns = new List<ImportColumn> { new ImportColumn { PropertyName = nameof(UnmappedTagsDto.Tags), Name = "标签" } }
            };

            var exception = await Assert.ThrowsAsync<ImportException>(
                async () => await TestHelpers.ImportAllAsync<UnmappedTagsDto>(importer, TestHelpers.ToStream(content), options));

            Assert.Contains("字面量类型", exception.Message);
        }

        [Fact]
        public async Task Import_ProgrammaticColumn_UnknownProperty_Throws()
        {
            var content = "编码\nD001";
            var options = new ImportOptions
            {
                Columns = new List<ImportColumn> { new ImportColumn { PropertyName = "不存在", Name = "编码" } }
            };

            var exception = await Assert.ThrowsAsync<ImportException>(
                async () => await TestHelpers.ImportAllAsync<PlainDto>(importer, TestHelpers.ToStream(content), options));

            Assert.Contains("不存在的属性", exception.Message);
        }

        [Fact]
        public async Task Import_ProgrammaticColumn_PropertyWithoutSetter_Throws()
        {
            // 直填只读属性的 PropertyInfo 同样快速失败（不依赖回查路径的属性池过滤）
            var content = "编码\nD001";
            var options = new ImportOptions
            {
                Columns = new List<ImportColumn>
                {
                    new ImportColumn { Name = "编码", Property = typeof(UnmappedReadOnlyDto).GetProperty(nameof(UnmappedReadOnlyDto.Code)) }
                }
            };

            var exception = await Assert.ThrowsAsync<ImportException>(
                async () => await TestHelpers.ImportAllAsync<UnmappedReadOnlyDto>(importer, TestHelpers.ToStream(content), options));

            Assert.Contains("不可写", exception.Message);
        }

        [Fact]
        public async Task Import_ProgrammaticColumn_PropertyNameReferencesReadOnly_Throws()
        {
            var content = "编码\nD001";
            var options = new ImportOptions
            {
                Columns = new List<ImportColumn> { new ImportColumn { PropertyName = nameof(UnmappedReadOnlyDto.Code), Name = "编码" } }
            };

            var exception = await Assert.ThrowsAsync<ImportException>(
                async () => await TestHelpers.ImportAllAsync<UnmappedReadOnlyDto>(importer, TestHelpers.ToStream(content), options));

            Assert.Contains("不可写", exception.Message);
        }

        [Fact]
        public async Task Import_ExtraHeaderColumn_Ignored()
        {
            var content = DeviceHeader + ",多余列\nD001,传感器,true,1,1,2026-01-02,00000000-0000-0000-0000-000000000001,online,1,多余值";

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
            Assert.False(row.RawValues.ContainsKey("多余列"));
        }

        [Fact]
        public async Task Import_AttributeColumnMissingInHeader_Skipped()
        {
            var content = "设备编码,设备名称\nD001,传感器";

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
            Assert.Equal(0, row.Data.Count);
            Assert.False(row.RawValues.ContainsKey("数量"));
        }

        [Fact]
        public async Task Import_BooleanValues_AcceptsTrueOneAndChinese()
        {
            var content = "设备编码,设备名称,启用\nD001,传感器,是\nD002,传感器,1\nD003,传感器,true\nD004,传感器,别的";

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content));

            Assert.True(rows[0].Data.Enabled);
            Assert.True(rows[1].Data.Enabled);
            Assert.True(rows[2].Data.Enabled);
            Assert.False(rows[3].Data.Enabled);
        }

        [Fact]
        public async Task Import_EmptyFields_UseTypeDefaults()
        {
            var content = "设备编码,数量,备注数\nD001,,";

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
            Assert.Equal(0, row.Data.Count);
            Assert.Null(row.Data.Remark);
        }

        [Fact]
        public async Task Import_LongAndDecimalFields_Converted()
        {
            var content = "L,M\n9223372036854775807,1.25";

            var rows = await TestHelpers.ImportAllAsync<TypesDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal(long.MaxValue, row.Data.L);
            Assert.Equal(1.25m, row.Data.M);
        }

        [Fact]
        public async Task Import_UnsignedAndSignedSmallIntegerFields_Converted()
        {
            var content = "SB,US,UI,UL\n-128,65535,4294967295,18446744073709551615";

            var rows = await TestHelpers.ImportAllAsync<TypesDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal(sbyte.MinValue, row.Data.SB);
            Assert.Equal(ushort.MaxValue, row.Data.US);
            Assert.Equal(uint.MaxValue, row.Data.UI);
            Assert.Equal(ulong.MaxValue, row.Data.UL);
        }

        [Fact]
        public async Task Import_NarrowingOverflow_CollectsError()
        {
            var content = "US,SB\n65536,300";

            var options = new ImportOptions { ErrorMode = ImportErrorMode.Collect };
            var rows = await TestHelpers.ImportAllAsync<TypesDto>(importer, TestHelpers.ToStream(content), options);

            var row = Assert.Single(rows);
            Assert.Equal(0, row.Data.US);
            Assert.Equal(0, row.Data.SB);
            Assert.Equal(2, row.Errors.Count);
        }

        [Fact]
        public async Task Import_DateTimeOffsetField_Parsed()
        {
            var content = "DTO\n2024-06-15T10:30:00+08:00";

            var rows = await TestHelpers.ImportAllAsync<TypesDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal(new DateTimeOffset(2024, 6, 15, 10, 30, 0, TimeSpan.FromHours(8)), row.Data.DTO);
        }

        [Fact]
        public async Task Import_TimeSpanField_Parsed()
        {
            var content = "TS\n1.02:03:04";

            var rows = await TestHelpers.ImportAllAsync<TypesDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal(new TimeSpan(1, 2, 3, 4), row.Data.TS);
        }

        [Fact]
        public async Task Import_NonLiteralFieldWithConverter_Converted()
        {
            var content = "标签\n\"A,B,C\"";

            var rows = await TestHelpers.ImportAllAsync<StringListDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal(new List<string> { "A", "B", "C" }, row.Data.Tags);
        }

        [Fact]
        public async Task Import_InvalidValues_SilentlyDefault()
        {
            var content = "设备编码,数量,编号,状态\nD001,abc,not-a-guid,不存在";

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal(0, row.Data.Count);
            Assert.Equal(Guid.Empty, row.Data.Uid);
            Assert.Equal(DeviceStatus.Online, row.Data.Status);
        }

        [Fact]
        public async Task Import_FormattedColumns_ParseExactWithFormat()
        {
            var content = "日期,价格\n20260102,1.50";

            var rows = await TestHelpers.ImportAllAsync<FormattedDto>(importer, TestHelpers.ToStream(content));

            var row = Assert.Single(rows);
            Assert.Equal(new DateTime(2026, 1, 2), row.Data.Day);
            Assert.Equal(1.5, row.Data.Price);
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
            var content = DeviceHeader + "\nD001,传感器,true,1,1,2026-01-02,00000000-0000-0000-0000-000000000001,online,1";

            var rows = await TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content), null);

            var row = Assert.Single(rows);
            Assert.Equal("D001", row.Data.Code);
        }

        [Fact]
        public async Task Import_CancelledToken_ThrowsOperationCanceledException()
        {
            var content = DeviceHeader + "\nD001,传感器,true,1,1,2026-01-02,00000000-0000-0000-0000-000000000001,online,1";
            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                TestHelpers.ImportAllAsync<DeviceDto>(importer, TestHelpers.ToStream(content), null, cts.Token));
        }

        [Fact]
        public async Task GenerateTemplate_BasicTemplate_WritesHeaderAndExampleRow()
        {
            var stream = await importer.GenerateTemplateAsync<DeviceDto>();

            using (var reader = new StreamReader(stream))
            {
                Assert.Equal(DeviceHeader, await reader.ReadLineAsync());
                Assert.Equal(
                    "示例文本,示例文本,true,100,99.99,2026-01-01,00000000-0000-0000-0000-000000000000,Online,100",
                    await reader.ReadLineAsync());
            }
        }

        [Fact]
        public async Task GenerateTemplate_IncludeExampleRowsFalse_WritesHeaderOnly()
        {
            var options = new TemplateOptions { IncludeExampleRows = false };

            var stream = await importer.GenerateTemplateAsync<DeviceDto>(options);

            using (var reader = new StreamReader(stream))
            {
                Assert.Equal(DeviceHeader, await reader.ReadLineAsync());
                Assert.Null(await reader.ReadLineAsync());
            }
        }

        [Fact]
        public async Task GenerateTemplate_NoImportColumns_ThrowsTemplateException()
        {
            var exception = await Assert.ThrowsAsync<TemplateException>(
                () => importer.GenerateTemplateAsync<PlainDto>());

            Assert.Contains("未声明导入列", exception.Message);
        }
    }
}
