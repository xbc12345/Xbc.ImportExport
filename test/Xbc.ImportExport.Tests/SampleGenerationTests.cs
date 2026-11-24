using Xbc.ImportExport.Csv;
using Xbc.ImportExport.Excel;

namespace Xbc.ImportExport.Tests
{
    /// <summary>样例文件生成测试（产物写入 artifacts/samples，供人工打开检查模板与导出效果；断言仅保证文件生成且非空）。</summary>
    public class SampleGenerationTests
    {
        [Fact]
        public async Task Generate_SampleFiles_ForManualInspection()
        {
            // 测试程序集输出于 artifacts/Debug/net8.0，向上两级即 artifacts 根
            var samplesDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "samples"));
            Directory.CreateDirectory(samplesDir);

            // 模板：DeviceDto（含枚举下拉、示例行、字段说明 Sheet）
            var excelImporter = new ExcelImporter();
            var csvImporter = new CsvImporter();
            await WriteFileAsync(Path.Combine(samplesDir, "DeviceDto-Template.xlsx"),
                () => excelImporter.GenerateTemplateAsync<DeviceDto>());
            await WriteFileAsync(Path.Combine(samplesDir, "DeviceDto-Template.csv"),
                () => csvImporter.GenerateTemplateAsync<DeviceDto>());

            // 导出：两行设备数据
            var data = TestHelpers.DataOf(
                new DeviceDto
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
                },
                new DeviceDto
                {
                    Code = "D002",
                    Name = "湿度传感器",
                    Enabled = false,
                    Count = 20,
                    Price = 2.5,
                    MadeAt = new DateTime(2026, 2, 3),
                    Uid = new Guid("00000000-0000-0000-0000-000000000002"),
                    Status = DeviceStatus.Offline,
                    Remark = 8
                });

            var excelExporter = new ExcelExporter();
            var csvExporter = new CsvExporter();
            await WriteFileAsync(Path.Combine(samplesDir, "DeviceDto-Export.xlsx"),
                () => excelExporter.ExportAsync(data, new ExportOptions()));
            await WriteFileAsync(Path.Combine(samplesDir, "DeviceDto-Export.csv"),
                () => csvExporter.ExportAsync(data, new ExportOptions()));
        }

        [Fact]
        public async Task Generate_ErrorReport_FromValidatedImport()
        {
            // 测试程序集输出于 artifacts/Debug/net8.0，向上两级即 artifacts 根
            var samplesDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "samples"));
            Directory.CreateDirectory(samplesDir);

            // 模拟用户回传的问题文件：干净行、数量解析错误、编码为空、日期解析错误、编码未建档
            var content = "设备编码,设备名称,启用,数量,价格,生产日期,编号,状态,备注数\r\n" +
                "D001,温度传感器,true,10,1.5,2026-01-02,00000000-0000-0000-0000-000000000001,online,5\r\n" +
                "D002,湿度传感器,true,abc,2.5,2026-02-03,00000000-0000-0000-0000-000000000002,offline,8\r\n" +
                ",压力传感器,true,5,3.5,2026-03-04,00000000-0000-0000-0000-000000000003,online,2\r\n" +
                "D003,流量传感器,true,8,4.5,非法日期,00000000-0000-0000-0000-000000000004,unknown,1\r\n" +
                "D004,液位传感器,false,15,9.9,2026-04-05,00000000-0000-0000-0000-000000000005,online,6";

            // 端到端管线：Collect 导入 → 链式 ValidateWith 业务校验（编码非空 + 须已建档）→ 错误回执
            var importer = new CsvImporter();
            var options = new ImportOptions { ErrorMode = ImportErrorMode.Collect };
            var knownCodes = new HashSet<string> { "D001", "D002", "D003" };

            var rows = new List<ParsedRow<DeviceDto>>();
            await foreach (var row in importer
                .ImportAsync<DeviceDto>(TestHelpers.ToStream(content), options)
                .ValidateWith(d => string.IsNullOrEmpty(d.Code) ? "设备编码不能为空" : null)
                .ValidateWith(d => !string.IsNullOrEmpty(d.Code) && !knownCodes.Contains(d.Code)
                    ? $"设备编码 {d.Code} 在系统中不存在"
                    : null))
            {
                rows.Add(row);
            }

            // 5 行数据：首行干净，其余 4 行各含错误（解析/空编码/日期解析/未建档）
            Assert.Equal(5, rows.Count);
            Assert.Equal(4, rows.Count(r => r.Errors.Count > 0));

            using (var report = await importer.GenerateErrorReportAsync(rows))
            using (var file = File.Create(Path.Combine(samplesDir, "DeviceDto-错误回执.csv")))
            {
                await report.CopyToAsync(file);
            }

            var reportPath = Path.Combine(samplesDir, "DeviceDto-错误回执.csv");
            Assert.True(new FileInfo(reportPath).Length > 0, $"错误回执文件应非空：{reportPath}");
        }

        private static async Task WriteFileAsync(string path, Func<Task<Stream>> generate)
        {
            using (var stream = await generate())
            {
                using (var file = File.Create(path))
                {
                    await stream.CopyToAsync(file);
                }
            }

            Assert.True(new FileInfo(path).Length > 0, $"样例文件应非空：{path}");
        }
    }
}
