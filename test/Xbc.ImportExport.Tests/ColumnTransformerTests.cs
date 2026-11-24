using System;
using Xbc.ImportExport;
using Xbc.ImportExport.Csv;

namespace Xbc.ImportExport.Tests
{
    /// <summary>
    /// <see cref="ExportOptions.ColumnTransformer"/> 列变换钩子测试。
    /// 覆盖：特性列经变换、程序化列经变换（调用方列实例不受污染）、变换不污染特性列类型缓存。
    /// </summary>
    public class ColumnTransformerTests
    {
        /// <summary>特性声明列测试实体。</summary>
        private class AttributeDto
        {
            [ExportColumn("编码", Order = 1)]
            public string Code { get; set; } = string.Empty;

            [ExportColumn("名称", Order = 2)]
            public string Name { get; set; } = string.Empty;
        }

        /// <summary>程序化列测试实体（属性不带 [ExportColumn]，列定义直填）。</summary>
        private class ProgrammaticDto
        {
            public string Phone { get; set; } = string.Empty;

            public string Name { get; set; } = string.Empty;
        }

        private static string[] ExportLines<T>(ExportOptions options, params T[] items) where T : class
        {
            var stream = new CsvExporter().ExportAsync(TestHelpers.DataOf(items), options).GetAwaiter().GetResult();
            return TestHelpers.ReadAll(stream)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        }

        [Fact]
        public void ColumnTransformer_AppliesToAttributeColumns()
        {
            var options = new ExportOptions
            {
                ColumnTransformer = column =>
                {
                    if (column.PropertyName == nameof(AttributeDto.Code))
                        column.Converter = value => "M-" + value;

                    return column;
                }
            };

            var lines = ExportLines(options, new AttributeDto { Code = "D001", Name = "温度传感器" });

            Assert.Equal("M-D001,温度传感器", lines[1]);
        }

        [Fact]
        public void ColumnTransformer_AppliesToProgrammaticColumns_CallerInstanceUnpolluted()
        {
            // 程序化列同样经变换；变换改写的是克隆副本，调用方列实例不受影响
            var phone = new ExportColumn { PropertyName = "Phone" };
            var options = new ExportOptions
            {
                Columns = { new ExportColumn { PropertyName = "Name" }, phone },
                ColumnTransformer = column =>
                {
                    if (column.PropertyName == "Phone")
                        column.Converter = value => "***";

                    return column;
                }
            };

            var lines = ExportLines(options, new ProgrammaticDto { Phone = "13812345678", Name = "设备" });

            Assert.Equal("设备,***", lines[1]);
            Assert.Null(phone.Converter);
        }

        [Fact]
        public void ColumnTransformer_DoesNotPolluteAttributeColumnCache()
        {
            // 带变换导出一次后，特性列的类型缓存不受影响：无变换的再次导出输出原始值
            var masked = new ExportOptions
            {
                ColumnTransformer = column =>
                {
                    if (column.PropertyName == nameof(AttributeDto.Name))
                        column.Converter = value => "***";

                    return column;
                }
            };
            ExportLines(masked, new AttributeDto { Code = "D001", Name = "温度传感器" });

            var lines = ExportLines(new ExportOptions(), new AttributeDto { Code = "D001", Name = "温度传感器" });

            Assert.Equal("D001,温度传感器", lines[1]);
        }
    }
}
