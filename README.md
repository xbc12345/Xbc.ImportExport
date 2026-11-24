<div align="center">

<img src="xbc.png" width="160" alt="Xbc.ImportExport" />

# Xbc.ImportExport

**.NET 轻量声明式导入导出库：实体一键转 Excel / CSV，流式低内存，错误回执闭环。**

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/xbc12345/Xbc.ImportExport/blob/master/LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET%208.0-blue)](https://github.com/xbc12345/Xbc.ImportExport)
<!-- TODO: 当前为静态占位 NuGet 徽章；包发布到 NuGet 后请替换为官方徽章，如 https://img.shields.io/nuget/v/Xbc.ImportExport.svg -->
[![NuGet](https://img.shields.io/badge/NuGet-3.0.0-blue)](https://www.nuget.org/packages/Xbc.ImportExport)

</div>

---

## 为什么做这个库

现存的 .NET 导入导出方案呈现两极：**直接用 CsvHelper / ClosedXML 手写**（表头映射、类型转换、错误定位、回执生成全部样板代码，每个业务模块重复一遍），**重型报表组件**（绑定特定对象模型或宿主环境，与服务端解耦困难）。Xbc.ImportExport 占据两者之间的空档：

- **声明式列映射**：`[ImportColumn]` / `[ExportColumn]` 特性声明即映射，列名、格式、默认值、转换器一处声明，导入绑定与导出、模板共用同一份声明
- **流式低内存**：导入逐行产出 `ParsedRow<T>`，导出消费 `IAsyncEnumerable<T>`，内存占用与文件行数无关
- **格式可插拔**：核心包零第三方依赖、不依赖 DI 与任何 Xbc 包，直接实例化即可使用；CSV / Excel 能力由独立提供者包承载，公共 API 不泄露 CsvHelper / ClosedXML 类型
- **错误可回执**：三态错误模式 + 业务校验装饰器 + 错误回执文件，Web 上传"导入 → 修正 → 重交"闭环开箱即用

## 特性

- **声明式列模型** —— 特性声明 / 强类型选项选择器 / 直填列定义三来源，程序化列与特性列择一生效，列名留空回退属性名，解析期快速失败校验
- **值解析白名单** —— 18 种字面量类型（含 `Nullable<>` 包装）；`decimal` 不经 `double` 中转防精度截断；枚举越界数值、窄化溢出、无符号负数拦截
- **三态错误模式** —— `Lenient` 静默回退 / `Strict` 快速失败定位行列 / `Collect` 记录错误继续产出；转换器异常与返回值类型不兼容走同一套分派
- **业务校验装饰器** —— `ValidateWith` 流式逐行校验，错误并入 `row.Errors` 与解析错误同处一行，可链式多条规则
- **错误回执** —— `GenerateErrorReportAsync` 生成与导入格式一致的回执文件（错误行高亮、错误信息列），修正后重新提交
- **导入模板生成** —— Excel 模板含表头样式、必填红标、枚举下拉校验、独立字段说明 Sheet；CSV 纯文本模板
- **导出能力** —— 强类型单元格（数值 / 布尔 / 日期 / 时间间隔）、`MaxRows` 上限、Excel 超限自动分片多工作表、CSV 公式注入防护与 RFC 4180 写出
- **异常体系收敛** —— CsvHelper / ClosedXML 异常统一包装为域内异常，底层异常经 `InnerException` 保留，不泄露到业务层

## 安装

核心包必装，CSV / Excel 提供者包按所用文件格式选装（版本号 `3.0.0` 为占位，以 NuGet 实际发布版本为准）：

```xml
<ItemGroup>
  <!-- 核心包（支持 netstandard2.0 / net8.0 / net461） -->
  <PackageReference Include="Xbc.ImportExport" Version="3.0.0" />

  <!-- 提供者包：按所用文件格式选装 -->
  <PackageReference Include="Xbc.ImportExport.Csv" Version="3.0.0" />
  <PackageReference Include="Xbc.ImportExport.Excel" Version="3.0.0" />
</ItemGroup>
```

## 包架构

| 包 | 职责 | 第三方依赖 |
| --- | --- | --- |
| `Xbc.ImportExport` | 核心包：`IImporter` / `IExporter` 抽象、列模型与选项、值解析器、业务校验装饰器、错误回执、异常体系 | 无 |
| `Xbc.ImportExport.Csv` | CSV 提供者包：`CsvImporter` / `CsvExporter` | `CsvHelper` |
| `Xbc.ImportExport.Excel` | Excel 提供者包：`ExcelImporter` / `ExcelExporter`，.xlsx 读写、工作表选择、分片导出 | `ClosedXML` |

ClosedXML 与 CsvHelper 的官方支持基线为 .NET Framework 4.6.2，net461 目标经 netstandard2.0 资产运行，可用但不在官方承诺范围。

## 快速开始

### 1. 导出

实体属性标记 `[ExportColumn]`，导出器接收异步数据流并返回文件流：

```csharp
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xbc.ImportExport;
using Xbc.ImportExport.Csv;    // Excel 场景换为 Xbc.ImportExport.Excel + new ExcelExporter()

public class DeviceDto
{
    [ExportColumn("设备编码", Order = 1)]
    public string Code { get; set; }

    [ExportColumn("设备名称", Order = 2)]
    public string Name { get; set; }

    [ExportColumn("启用", Order = 3)]
    public bool Enabled { get; set; }
}

public class ExportDemo
{
    public async Task ExportAsync()
    {
        var exporter = new CsvExporter();
        var stream = await exporter.ExportAsync(LoadDevices(), new ExportOptions());

        using (stream)
        using (var file = File.Create("devices.csv"))
        {
            await stream.CopyToAsync(file);
        }
    }

    private async IAsyncEnumerable<DeviceDto> LoadDevices()
    {
        yield return new DeviceDto { Code = "D001", Name = "温度传感器", Enabled = true };
        yield return new DeviceDto { Code = "D002", Name = "湿度传感器", Enabled = false };
    }
}
```

### 2. 导入

导入器流式逐行产出 `ParsedRow<T>`（实体 + 行号 + 原始值字典 + 行级错误），调用方按需消费并自行入库：

```csharp
using System.IO;
using System.Threading.Tasks;
using Xbc.ImportExport;
using Xbc.ImportExport.Csv;

public class DeviceImportDto
{
    [ImportColumn("设备编码")]
    public string Code { get; set; }

    [ImportColumn("设备名称")]
    public string Name { get; set; }

    [ImportColumn("启用", DefaultValue = "true")]
    public bool Enabled { get; set; }
}

public class ImportDemo
{
    public async Task ImportAsync(Stream csvStream)
    {
        var importer = new CsvImporter();
        var options = new ImportOptions { ErrorMode = ImportErrorMode.Collect };

        await foreach (var row in importer.ImportAsync<DeviceImportDto>(csvStream, options))
        {
            if (row.Errors.Count > 0)
            {
                // row.RowNumber 定位坏行，row.RawValues 按列名保留原始文本
                Console.WriteLine($"第 {row.RowNumber} 行解析失败：{string.Join("；", row.Errors)}");
            }
            else
            {
                await SaveAsync(row.Data);
            }
        }
    }
}
```

### 3. 生成导入模板

模板按 `[ImportColumn]` 声明生成，供填写方下载后填写再导入：

```csharp
using System.IO;
using System.Threading.Tasks;
using Xbc.ImportExport;
using Xbc.ImportExport.Excel;

public class TemplateDemo
{
    public async Task GenerateAsync()
    {
        var importer = new ExcelImporter();
        using (var stream = await importer.GenerateTemplateAsync<DeviceImportDto>())
        using (var file = File.Create("设备导入模板.xlsx"))
        {
            await stream.CopyToAsync(file);
        }
    }
}
```

CSV 模板用 `new CsvImporter().GenerateTemplateAsync<T>()` 生成，同样按特性声明产出表头与示例行。

## 列声明

列定义有三个来源：特性声明、强类型选项、直填列定义。程序化列（`Columns` 有列时）与特性列择一生效，完全替代特性列而非合并；三个来源的列名留空一律回退属性名。

### 特性声明

`[ImportColumn]`：

| 属性 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `Name` | `string` | 属性名 | 列名（表头匹配名） |
| `Format` | `string` | 空 | 解析格式（如 `yyyy-MM-dd`） |
| `DefaultValue` | `object` | null | 单元格为空时的回退值 |
| `ConverterType` | `Type` | null | 导入转换器，实现 `IImportValueConverter`，须有无参构造且无状态 |
| `Order` | `int` | 0 | 模板列序（1 起；导入绑定按列名匹配，与顺序无关） |
| `IsRequired` | `bool` | false | 必填声明：模板必填标记（红色表头与字段说明）+ 导入必填校验（空单元格且未声明默认值按 `ErrorMode` 分派） |
| `Width` | `double` | 15 | Excel 模板列宽（字符宽度） |
| `Description` | `string` | null | 字段说明（Excel 模板的说明 Sheet） |

`[ExportColumn]`：

| 属性 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `Name` | `string` | 属性名 | 表头显示名 |
| `Order` | `int` | 0 | 列序（1 起，正序号升序在前；0 或负值按声明顺序排后） |
| `Format` | `string` | 空 | 格式化（如 `yyyy-MM-dd`、`N2`） |
| `Width` | `double` | 15 | Excel 导出列宽（字符宽度） |
| `ConverterType` | `Type` | null | 导出转换器，实现 `IExportValueConverter`，须有无参构造且无状态 |

### 强类型选项

经属性选择器链式声明（`ImportOptions<T>` / `ExportOptions<T>`）：

```csharp
var exportOptions = new ExportOptions<DeviceDto>()
    .Column(x => x.Code, "编码")
    .Column(x => x.Name, "名称", c => c.Order = 2)
    .Column(x => x.Enabled, "启用", c => c.Converter = v => (bool)v ? "启用" : "停用");

var importOptions = new ImportOptions<DeviceImportDto>()
    .Column(x => x.Code, "设备编码")
    .Column(x => x.Enabled, "启用", c => c.DefaultValue = true);
```

### 直填列定义

直接构造 `ImportColumn` / `ExportColumn` 加入 `Columns`，适合转换器委托等动态场景：

```csharp
// 标签列为集合类型，不走特性声明，经程序化列 + 转换器导入
public class DeviceTagDto
{
    [ImportColumn("设备编码")]
    public string Code { get; set; }

    public List<string> Tags { get; set; }
}

var options = new ImportOptions
{
    Columns = new List<ImportColumn>
    {
        // 列名留空回退属性名，表头匹配 "Code"
        new ImportColumn { PropertyName = "Code" },
        // 逗号分隔文本 → List<string>
        new ImportColumn { PropertyName = "Tags", Converter = v => new List<string>(v.Split(',')) }
    }
};
```

选择器仅支持成员访问表达式（值类型属性经编译器生成的 `Convert` 节点自动解包），其他表达式（方法调用、计算式）抛 `ArgumentException`。

程序化列在解析期快速失败校验：属性不存在、`Property` 与 `PropertyName` 不一致、同一属性重复声明、列名重复、属性不可写（导入）/ 不可读（导出）、非字面量类型且未设转换器，均抛 `ImportException` / `ExportException`。

## 值解析（导入）

字面量类型白名单（含 `Nullable<>` 包装）：`string`、`bool`、`DateTime`、`DateTimeOffset`、`TimeSpan`、`Guid`、`byte`、`sbyte`、`short`、`ushort`、`int`、`uint`、`long`、`ulong`、`float`、`double`、`decimal`、枚举。

各类型解析行为：

| 类型 | 行为 |
| --- | --- |
| 整型家族 | InvariantCulture 整数样式；无符号类型天然拒负数；窄化溢出（如 65536 给 `ushort`）按解析失败处理 |
| `float` / `double` / `decimal` | 各自直接 `TryParse`（decimal 不经 double 中转，避免精度截断） |
| `bool` | `true` / `1` / `yes` / `是` 与 `false` / `0` / `no` / `否`，大小写不敏感 |
| `DateTime` / `DateTimeOffset` | 声明 `Format` 时按 `TryParseExact`，否则按 InvariantCulture `TryParse`；`DateTimeOffset` 保留偏移，无偏移文本按当前时区补齐 |
| `TimeSpan` | 声明 `Format` 时按 `TryParseExact`，否则 `TryParse` |
| 枚举 | 名称（大小写不敏感）或数值，须落在已定义值内（越界数值按失败处理）；`[Flags]` 枚举按标准语义放行组合值与位掩码（如 `Read, Write`） |

非字面量类型（集合、复杂对象等）必须声明转换器，否则解析期快速失败；`object` 类型属性禁止透传导入。

错误处理三态（`ImportOptions.ErrorMode`）：

| 模式 | 行为 |
| --- | --- |
| `Lenient`（默认） | 解析失败静默回退默认值，不产生错误记录 |
| `Strict` | 任一值失败立即抛 `ImportDataException`（行号 + 列名），终止导入 |
| `Collect` | 失败记入 `ParsedRow.Errors` 并回退默认值，继续产出该行 |

统一按 `ErrorMode` 分派的失败场景：文本不可解析、无符号类型收到负数、窄化溢出、必填列空单元格（`IsRequired` 且未声明 `DefaultValue`；`Lenient` 静默回退类型默认值不算拦截）。必填列在表头中整列缺失（用错模板或列名被改）属文件结构错误，任何模式下直接抛 `ImportException` 快速失败；非必填列缺表头维持缺列跳过。

空单元格走默认值路径：声明了 `DefaultValue` 且与属性类型兼容则直用，字符串默认值按解析路径处理（解析失败静默回退类型默认值），未声明则用类型默认值。转换器仅对非空文本调用。

## 表头与行语义

- 表头行号由 `ImportOptions.StartRow` 指定（默认 1，小于 1 视为 1），数据从表头下一行开始
- 表头匹配忽略大小写与首尾空白；空白标题列不导入，不影响其他列
- 表头是导入的先决契约：无有效表头（空文件、起始行超出数据范围、表头列名全为空白）抛 `ImportException` 快速失败
- 两列同名标题取最后一列；文件中缺失的声明列静默跳过，映射按列名匹配、与列顺序无关
- 空行判定为整行所有列（含未映射列）均为空白；`SkipEmptyRows`（默认 true）跳过空行，false 时产出默认值行（声明默认值生效）
- CSV 截断行（字段数少于表头）不报错，缺失字段按空单元格走默认值路径
- CSV 文本按 RFC 4180 解析（引号内逗号、换行合法），固定按 UTF-8 读取（自动识别 UTF-8 / UTF-16 BOM）；其他编码（如 GB18030）的文件由调用方预先转码为 UTF-8 再导入
- Excel 工作表由 `ImportOptions.SheetName` 选择（null 或空 = 第一个工作表，名称不存在抛 `ImportException` 并列出可用工作表）；CSV 无工作表概念忽略此项
- `ImportOptions.MaxRows` 限制导入数据行数（默认 100000，小于等于 0 不限，与导出侧 `MaxRows` 语义对称）：超过上限抛 `ImportDataException` 并定位行号，跳过的空行不计入
- 导入流式产出，逐行解析，内存占用与文件行数无关

## 导出行为

- null 属性值直接写空单元格 / 空字段，不经过转换器
- CSV 公式防护：文本以 `=`、`+`、`-`、`@`、制表符、回车开头时前置 `'`，防止 Excel 公式注入；Excel 导出为强类型文本单元格，天然无此风险
- CSV 写出遵循 RFC 4180（引号转义、CRLF 换行），UTF-8 带 BOM
- Excel 单元格按类型落值：数值、布尔、日期、时间间隔为强类型单元格（`Format` 映射为数字格式），字符串、`Guid`、枚举名称按文本
- `DateTimeOffset` 按钟面值写出（偏移不保留），与导入解析按钟面往返对齐
- `MaxRows` 默认 100000，小于等于 0 表示不限；数据超过上限抛 `ExportException`，不产出部分文件
- `MaxRowsPerSheet` 控制 Excel 单工作表数据行上限（默认 0 不分片）：超出自动分片为新工作表，命名 基名/基名-2/基名-3（工作表名上限 31 字符），每个工作表重复表头与列宽；CSV 忽略此项
- `IncludeHeader`（默认 true）控制表头行；`SheetName`（默认 `Sheet1`）设置 Excel 工作表名，CSV 忽略
- 程序化列与特性列均按 `Order` 排序（显式正序号升序在前，0 或负值按声明顺序排后）

## 模板生成

模板列布局仅由 `[ImportColumn]` 声明决定，程序化列不参与模板生成。示例行固定 1 行，取值优先级：枚举允许值首个，TimeSpan 列展示 `01:02:03`，其余按数据类型推断（如字符串 `示例文本`、整数 `100`、日期 `2026-01-01`）。

Excel 模板能力（`TemplateOptions`，Sheet 名默认 `导入模板`）：

| 能力 | 说明 |
| --- | --- |
| 表头样式 | 加粗、浅蓝背景，必填列红色字体 |
| 列宽 | 按 `Width`（默认 15） |
| 数据校验 | 枚举列生成下拉列表（第 2 至 1000 行，警告样式，允许强制输入） |
| 示例行 | 表头下 1 行（`IncludeExampleRows`，默认 true） |
| 字段说明 | 独立 Sheet，含列名 / 是否必填 / 数据类型 / 格式说明 / 允许值 / 说明六列（`IncludeFieldDescription`，默认 true） |

CSV 模板为纯文本（表头 + 可选示例行），写出带 UTF-8 BOM；列宽、数据校验、字段说明与 Sheet 名不适用，自动忽略。

## 业务校验与错误回执

Web 上传场景的标准管线：导入（`Collect` 模式）→ `ValidateWith` 业务校验 → 错误行生成回执回传填写方。

### ValidateWith 装饰器

`ValidateWith` 是 `IAsyncEnumerable<ParsedRow<T>>` 的流式装饰器，逐行调用业务校验委托，错误信息并入 `row.Errors`（与解析错误同处一行，不拦截行产出）：

```csharp
await foreach (var row in importer
    .ImportAsync<DeviceImportDto>(csvStream, options)
    .ValidateWith(d => deviceService.Exists(d.Code)
        ? null
        : $"设备编码 {d.Code} 不存在"))
{
    // row.Errors 同时包含解析错误与业务校验错误
}
```

校验委托返回单条错误信息，null 或空串视为通过；多条业务规则链式多个 `ValidateWith`（错误按校验器顺序合并）；`Data` 为 null 的行原样透传不参与校验。

### 错误回执 GenerateErrorReportAsync

`importer.GenerateErrorReportAsync(rows)` 将错误行集合生成回执，格式与导入器一致（CSV 为 RFC 4180 文本 UTF-8 带 BOM；Excel 为 xlsx 工作簿，错误行浅黄底、错误信息列红字），回传填写方修正后重新提交：

```csharp
var rows = await CollectRowsAsync(importer, csvStream, options);
var errorRows = rows.Where(r => r.Errors.Count > 0).ToList();
if (errorRows.Count > 0)
{
    // 回执列：行号 + 原始列（错误行 RawValues 键的首次出现顺序）+ 错误信息（多条以；串联）
    using (var report = await importer.GenerateErrorReportAsync(rows))
    {
        await fileService.SaveAsync("导入错误回执.csv", report);
    }
}
```

仅错误行进入回执（干净行不包含）；无错误行时仅输出表头。公式触发文本统一加 ' 前缀防护（CSV 为字面字符，Excel 为 quotePrefix 样式）；CSV 回执字段按 RFC 4180 转义（含逗号、引号、换行的字段引号包裹并双写内部引号）。

## 异常体系

```text
ImportExportException（抽象基类）
├─ ImportException               导入配置 / 流 / 表头级失败
│  └─ ImportDataException        数据行解析失败（Strict 模式），含 RowNumber 与 ColumnName
├─ ExportException               导出失败
└─ TemplateException             模板生成失败（如类型未声明 [ImportColumn]）
```

提供者包将 CsvHelper / ClosedXML 异常统一包装为上述域内异常，底层异常经 `InnerException` 保留，不泄露到业务层。

## 线程安全

`CsvImporter` / `CsvExporter` / `ExcelImporter` / `ExcelExporter` 无实例状态，单实例可并发复用。列解析器为静态类，按类型缓存列定义（`ConcurrentDictionary`），线程安全。

## 常见陷阱

| 陷阱 | 后果 | 解决方案 |
| --- | --- | --- |
| 程序化列与特性列并存 | `Columns` 有列时特性列完全失效 | 择一声明，程序化列须覆盖全部所需列 |
| 非字面量类型属性未声明转换器 | 解析期抛 `ImportException` / `ExportException` | 声明 `ConverterType` 或设置 `Converter` 委托 |
| 忘记引用提供者包 | 无法实例化导入 / 导出器 | 按格式引用 `Xbc.ImportExport.Csv` / `Xbc.ImportExport.Excel` |
| 导出数据超过 `MaxRows` | 抛 `ExportException` | 调大 `MaxRows` 或设为 0（不限） |
| 日期列未声明 `Format` | 非标准格式解析失败，按 `ErrorMode` 分派 | 显式声明 `Format`（如 `yyyy-MM-dd`） |
| 转换器有状态或缺少无参构造 | 特性路径实例化失败或行为异常 | 转换器保持无状态，特性路径每类型仅实例化一次 |
| 必填列空单元格且 `ErrorMode` 为 `Lenient` | 静默回退类型默认值不算拦截，错误被吞 | 必填列需感知时用 `Strict` 或 `Collect` 模式 |
| 非 UTF-8 编码的 CSV 文件（如 GB18030） | 按 UTF-8 解码中文静默乱码，不报错 | 调用方预先转码为 UTF-8（或读取时用 `StreamReader` 指定编码后传入已解码流） |
| 期望框架直接入库 | 框架只解析不落库 | 调用方消费 `ParsedRow<T>` 自行入库 |

## 已知限制

- 模板生成仅支持特性声明列，程序化列暂不支持模板
- float 溢出文本（如 `3.5E+38`）在 .NET 8 按 IEEE 754 解析为 `PositiveInfinity`（不算失败），在 .NET Framework 4.6.1 按解析失败处理，行为随运行时而异

## License

本项目基于 [MIT License](LICENSE) 开源发布。
