using System.Collections.Concurrent;
using System.Reflection;

namespace Xbc.ImportExport
{
    /// <summary>
    /// 导出列解析器（基于 <see cref="ExportColumnAttribute"/> 解析导出列定义，按类型缓存）。
    /// </summary>
    public static class ExportColumnResolver
    {
        private static readonly ConcurrentDictionary<Type, List<ExportColumn>> exportColumnCache
            = new ConcurrentDictionary<Type, List<ExportColumn>>();

        /// <summary>解析生效的导出列定义（程序化列优先并校验，否则回退特性解析；导出侧的列定义选定入口，对应导入侧 BuildMapping 的列定义选定部分）。</summary>
        internal static List<ExportColumn> ResolveEffectiveColumns(List<ExportColumn> programmaticColumns, Type targetType)
        {
            // ExportOptions.Columns 默认初始化为空 List<ExportColumn>（非 null），必须用 Count==0 判断"未指定"，单纯 ?? 无法触发回退
            if (programmaticColumns != null && programmaticColumns.Count > 0)
                return ResolveProgrammaticColumns(targetType, programmaticColumns);

            return ResolveExportColumns(targetType);
        }

        /// <summary>解析生效列定义并在配置 <see cref="ExportOptions.ColumnTransformer"/> 时逐列应用变换
        /// （变换收到克隆隔离的列副本，返回替换定义或 null 保留原列；不触碰按类型缓存的特性列与调用方列实例，
        /// 列序保持解析序——无法删列/重排/注入；导出器的列定义选定入口）。</summary>
        internal static List<ExportColumn> ResolveAndTransformColumns(ExportOptions options, Type targetType)
        {
            var columns = ResolveEffectiveColumns(options?.Columns, targetType);
            var transformer = options?.ColumnTransformer;
            if (transformer == null)
                return columns;

            var result = new List<ExportColumn>(columns.Count);
            foreach (var column in columns)
            {
                var transformed = transformer(CloneColumn(column));
                result.Add(transformed ?? column);
            }

            return result;
        }

        /// <summary>克隆列定义（列修饰的隔离边界；ExportColumn 新增字段时在此集中同步）。</summary>
        private static ExportColumn CloneColumn(ExportColumn column)
        {
            return new ExportColumn
            {
                PropertyName = column.PropertyName,
                Property = column.Property,
                Name = column.Name,
                Order = column.Order,
                Format = column.Format,
                Width = column.Width,
                Converter = column.Converter
            };
        }

        /// <summary>解析特性声明的导出列定义（按 <see cref="ExportColumnAttribute.Order"/> 升序，Order = 0 按声明顺序排后，决定导出文件列序）。
        /// 结果按类型缓存并返回共享实例，调用方只读；需加工列定义请经 <see cref="ExportOptions.ColumnTransformer"/>（框架克隆隔离后逐列回调）。</summary>
        internal static List<ExportColumn> ResolveExportColumns(Type targetType)
        {
            return exportColumnCache.GetOrAdd(targetType, t =>
            {
                var columns = new List<ExportColumn>();
                var seenNames = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

                foreach (var prop in ColumnResolverCore.GetInstanceProperties(t))
                {
                    var attr = prop.GetCustomAttribute<ExportColumnAttribute>();
                    if (attr == null)
                        continue;

                    if (!prop.CanRead)
                        throw new ExportException($"类型 '{t}' 的属性 '{prop.Name}' 标记了 [ExportColumn] 但不可读（缺少 getter）");

                    if (attr.ConverterType == null && !ColumnResolverCore.IsLiteralType(prop.PropertyType))
                        throw new ExportException(
                            $"类型 '{t}' 中 [ExportColumn] 声明的属性 '{prop.Name}' 类型 '{ColumnResolverCore.GetDisplayName(prop.PropertyType)}' 不是受支持的字面量类型（请声明 ConverterType 或改用字面量类型）");

                    // 列名重复防线（对齐程序化路径 seenNames 校验）：两个属性声明同名列会产出歧义表头，往返导入无法建立唯一映射，快速失败
                    var name = string.IsNullOrEmpty(attr.Name) ? prop.Name : attr.Name;
                    if (!seenNames.TryGetValue(name, out var firstOwner))
                    {
                        seenNames.Add(name, prop);
                    }
                    else
                    {
                        throw new ExportException($"类型 '{t}' 的导出列名 '{name}' 重复（属性 '{firstOwner.Name}' 与 '{prop.Name}' 均声明了该列名，列名在文件内必须唯一）");
                    }

                    columns.Add(new ExportColumn
                    {
                        PropertyName = prop.Name,
                        Property = prop,
                        Name = name,
                        Order = attr.Order,
                        Format = attr.Format ?? string.Empty,
                        Width = attr.Width,
                        Converter = CreateExportConverter(attr.ConverterType, t)
                    });
                }

                return ColumnResolverCore.SortByOrder(columns, c => c.Order);
            });
        }

        /// <summary>解析程序化导出列（与特性路径 <see cref="ResolveExportColumns"/> 对称的程序化列定义入口：校验冲突、重复绑定、可读性与字面量类型并按名回查补齐属性，按 Order 排序，引用不存在属性时快速失败）。</summary>
        private static List<ExportColumn> ResolveProgrammaticColumns(Type targetType, List<ExportColumn> columns)
        {
            var properties = ColumnResolverCore.GetInstanceProperties(targetType);
            var result = new List<ExportColumn>();
            var seenProperties = new HashSet<PropertyInfo>();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var column in columns)
            {
                // Property 与 PropertyName 都提供且不一致时快速失败（特性与强类型路径二者同源恒一致，仅手工构造可能冲突）
                if (column.Property != null
                    && !string.IsNullOrEmpty(column.PropertyName)
                    && !string.Equals(column.Property.Name, column.PropertyName, StringComparison.OrdinalIgnoreCase))
                    throw new ExportException($"程序化导出列 '{column.Name}' 的 PropertyName 与 Property '{column.Property.Name}' 不一致");

                // 绑定属性解析：直填优先，否则按名回查（全量属性池）
                var property = column.Property ?? properties.FirstOrDefault(p => string.Equals(p.Name, column.PropertyName, StringComparison.OrdinalIgnoreCase));
                if (property == null)
                    throw new ExportException($"程序化导出列 '{column.Name}' 引用了类型 '{targetType}' 上不存在的属性 '{column.PropertyName}'");

                // 列名空则回退属性名（与特性声明、强类型选项路径统一；空名列会写出空表头，往返导入时该列必丢）
                var name = string.IsNullOrEmpty(column.Name) ? property.Name : column.Name;

                // 重复声明防线（特性路径由 AllowMultiple = false 天然防重，程序化路径在此对齐）：
                // 同一属性重复绑定会导致同值双列，列名重复会产出歧义表头，均快速失败
                if (!seenProperties.Add(property))
                    throw new ExportException($"程序化导出列 '{name}' 重复绑定属性 '{property.Name}'（同一属性只允许声明一次）");

                if (!seenNames.Add(name))
                    throw new ExportException($"程序化导出列名 '{name}' 重复");

                if (!property.CanRead)
                    throw new ExportException($"程序化导出列 '{name}' 引用的属性 '{property.Name}' 不可读（缺少 getter）");

                if (column.Converter == null && !ColumnResolverCore.IsLiteralType(property.PropertyType))
                    throw new ExportException($"程序化导出列 '{name}' 的属性 '{property.Name}' 类型 '{ColumnResolverCore.GetDisplayName(property.PropertyType)}' 不是受支持的字面量类型（请设置 Converter 或改用字面量类型）");

                // 直填且列名非空时直接引用原实例；回查补齐或列名回退时克隆（不改动调用方实例）
                if (property == column.Property && !string.IsNullOrEmpty(column.Name))
                {
                    result.Add(column);
                    continue;
                }

                // 克隆走统一出口，回查补齐与列名回退的字段在此覆写（不改动调用方实例）
                var clone = CloneColumn(column);
                clone.PropertyName = property.Name;
                clone.Property = property;
                clone.Name = name;
                result.Add(clone);
            }

            // 程序化列同样按 Order 排序（对齐特性路径，Order = 0 或负值按声明顺序排后）
            return ColumnResolverCore.SortByOrder(result, c => c.Order);
        }

        /// <summary>实例化特性声明的导出转换器并包装为委托（列定义按类型缓存，转换器仅首次解析时实例化一次）。</summary>
        private static Func<object, object> CreateExportConverter(Type converterType, Type targetType)
        {
            if (converterType == null)
                return null;

            var converter = (IExportValueConverter)ColumnResolverCore.CreateConverterInstance(
                converterType,
                targetType,
                typeof(IExportValueConverter),
                message => new ExportException(message));
            return converter.Convert;
        }
    }
}
