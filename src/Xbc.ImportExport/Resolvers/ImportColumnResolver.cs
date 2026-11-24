using System.Collections.Concurrent;
using System.Reflection;

namespace Xbc.ImportExport
{
    /// <summary>
    /// 导入列解析器（基于 <see cref="ImportColumnAttribute"/> 解析导入绑定列与导入模板列，按类型缓存；并从表头与列定义（程序化列或特性声明列）构建文件列映射，供导入器绑定使用）。
    /// </summary>
    public static class ImportColumnResolver
    {
        private static readonly ConcurrentDictionary<Type, List<ImportColumn>> importColumnCache
            = new ConcurrentDictionary<Type, List<ImportColumn>>();

        private static readonly ConcurrentDictionary<Type, List<TemplateColumn>> templateColumnCache
            = new ConcurrentDictionary<Type, List<TemplateColumn>>();

        /// <summary>构建文件列映射（程序化列与特性声明列择一生效：程序化列校验并规范化后完全替代特性列；列名匹配表头，非必填列缺列跳过，必填列表头缺失属文件结构错误快速失败）。</summary>
        internal static List<ImportColumnMapping> BuildMapping(
            Type targetType,
            List<ImportColumn> programmaticColumns,
            Dictionary<string, int> headerMap)
        {
            // 列定义择一：程序化列（校验并规范化，属性恒解析完毕）或特性声明列（已缓存）
            var columns = (programmaticColumns != null && programmaticColumns.Count > 0)
                ? ResolveProgrammaticColumns(targetType, programmaticColumns)
                : ResolveImportColumns(targetType);

            var result = new List<ImportColumnMapping>();

            // 按主名匹配表头；必填列未命中属文件结构错误（用错模板或列名被改，不归 ErrorMode 管辖），非必填列缺列跳过
            // （表头有效性由导入器在表头解析处校验，此处 headerMap 恒非空）
            foreach (var col in columns)
            {
                if (!headerMap.TryGetValue(col.Name, out var colIndex))
                {
                    if (col.IsRequired)
                        throw new ImportException($"必填列 '{col.Name}' 在表头中缺失（请核对文件列名与导入模板一致）");

                    continue;
                }

                result.Add(new ImportColumnMapping
                {
                    Column = col,
                    ColumnIndex = colIndex
                });
            }

            return result;
        }

        /// <summary>
        /// 解析导入绑定列（按 <see cref="ImportColumnAttribute.Order"/> 升序，Order = 0 按声明顺序排后，供导入器构建属性映射；按类型缓存，不向外部暴露可变缓存）。
        /// </summary>
        internal static List<ImportColumn> ResolveImportColumns(Type targetType)
        {
            return importColumnCache.GetOrAdd(targetType, t => ResolveDeclaredColumns(t)
                .Select(d => ToImportColumn(d, t))
                .ToList());
        }

        /// <summary>解析导入模板列（列布局与展示信息，按 <see cref="ImportColumnAttribute.Order"/> 升序，Order = 0 按声明顺序排后，供模板生成器使用；按类型缓存，不向外部暴露可变缓存）。</summary>
        internal static List<TemplateColumn> ResolveTemplateColumns(Type targetType)
        {
            return templateColumnCache.GetOrAdd(targetType, t => ResolveDeclaredColumns(t)
                .Select(d => ToTemplateColumn(d))
                .ToList());
        }

        /// <summary>扫描特性声明的导入列（校验 setter 与字面量类型后按 Order 排序，绑定列与模板列投影的共同来源）。</summary>
        private static List<DeclaredImportColumn> ResolveDeclaredColumns(Type targetType)
        {
            var declared = new List<DeclaredImportColumn>();
            foreach (var prop in ColumnResolverCore.GetInstanceProperties(targetType))
            {
                var attr = prop.GetCustomAttribute<ImportColumnAttribute>();
                if (attr == null)
                    continue;

                if (!prop.CanWrite)
                    throw new ImportException($"类型 '{targetType}' 的属性 '{prop.Name}' 标记了 [ImportColumn] 但不可写（缺少 setter）");

                if (attr.ConverterType == null && !ColumnResolverCore.IsLiteralType(prop.PropertyType))
                    throw new ImportException($"类型 '{targetType}' 中 [ImportColumn] 声明的属性 '{prop.Name}' 类型 '{ColumnResolverCore.GetDisplayName(prop.PropertyType)}' 不是受支持的字面量类型（请声明 ConverterType 或改用字面量类型）");

                declared.Add(new DeclaredImportColumn
                {
                    Property = prop,
                    Attribute = attr
                });
            }

            return ColumnResolverCore.SortByOrder(declared, d => d.Attribute.Order);
        }

        /// <summary>投影为导入绑定列（表头匹配与值绑定信息）。</summary>
        private static ImportColumn ToImportColumn(DeclaredImportColumn d, Type targetType)
        {
            return new ImportColumn
            {
                Name = d.GetColumnName(),
                PropertyName = d.Property.Name,
                Property = d.Property,
                Format = d.Attribute.Format ?? string.Empty,
                DefaultValue = d.Attribute.DefaultValue,
                IsRequired = d.Attribute.IsRequired,
                Converter = CreateImportConverter(d.Attribute.ConverterType, targetType)
            };
        }

        /// <summary>投影为导入模板列（列布局与展示信息，枚举列填充允许值，TimeSpan 列标记示例格式）。</summary>
        private static TemplateColumn ToTemplateColumn(DeclaredImportColumn d)
        {
            var underlying = Nullable.GetUnderlyingType(d.Property.PropertyType) ?? d.Property.PropertyType;
            var column = new TemplateColumn
            {
                Name = d.GetColumnName(),
                PropertyName = d.Property.Name,
                IsRequired = d.Attribute.IsRequired,
                Format = d.Attribute.Format ?? string.Empty,
                Width = d.Attribute.Width,
                Comment = d.Attribute.Description ?? string.Empty,
                DataType = ResolveColumnType(underlying),
                IsTimeSpanColumn = underlying == typeof(TimeSpan)
            };

            if (column.DataType == TemplateColumnType.Enum)
                column.AllowedValues = new List<string>(Enum.GetNames(underlying));

            return column;
        }

        /// <summary>实例化特性声明的导入转换器并包装为委托（列定义按类型缓存，转换器仅首次解析时实例化一次）。</summary>
        private static Func<string, object> CreateImportConverter(Type converterType, Type targetType)
        {
            if (converterType == null)
                return null;

            var converter = (IImportValueConverter)ColumnResolverCore.CreateConverterInstance(
                converterType,
                targetType,
                typeof(IImportValueConverter),
                message => new ImportException(message));
            return converter.Convert;
        }

        /// <summary>解析模板列数据类型（可空类型按底层类型判断，未识别的转换器列按字符串展示）。</summary>
        private static TemplateColumnType ResolveColumnType(Type propertyType)
        {
            var underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            if (underlying == typeof(string) || underlying == typeof(TimeSpan))
                return TemplateColumnType.String;

            if (underlying == typeof(int)
                || underlying == typeof(uint)
                || underlying == typeof(short)
                || underlying == typeof(ushort)
                || underlying == typeof(byte)
                || underlying == typeof(sbyte))
                return TemplateColumnType.Integer;

            if (underlying == typeof(long) || underlying == typeof(ulong))
                return TemplateColumnType.Long;

            if (underlying == typeof(double) || underlying == typeof(float) || underlying == typeof(decimal))
                return TemplateColumnType.Number;

            if (underlying == typeof(bool))
                return TemplateColumnType.Boolean;

            if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset))
                return TemplateColumnType.DateTime;

            if (underlying == typeof(Guid))
                return TemplateColumnType.Guid;

            if (underlying.IsEnum)
                return TemplateColumnType.Enum;

            return TemplateColumnType.String;
        }

        /// <summary>解析程序化导入列（与特性路径 <see cref="ResolveDeclaredColumns"/> 对称的程序化列定义入口：校验冲突、重复绑定与可写性并按名回查补齐属性；非字面量类型且无转换器时快速失败）。</summary>
        private static List<ImportColumn> ResolveProgrammaticColumns(Type targetType, List<ImportColumn> columns)
        {
            var properties = ColumnResolverCore.GetInstanceProperties(targetType);
            var result = new List<ImportColumn>();
            var seenProperties = new HashSet<PropertyInfo>();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var column in columns)
            {
                // Property 与 PropertyName 都提供且不一致时快速失败（特性与强类型路径二者同源恒一致，仅手工构造可能冲突）
                if (column.Property != null
                    && !string.IsNullOrEmpty(column.PropertyName)
                    && !string.Equals(column.Property.Name, column.PropertyName, StringComparison.OrdinalIgnoreCase))
                    throw new ImportException($"程序化导入列 '{column.Name}' 的 PropertyName 与 Property '{column.Property.Name}' 不一致");

                // 绑定属性解析：直填优先，否则按名回查（全量属性池）
                var property = column.Property ?? properties.FirstOrDefault(p => string.Equals(p.Name, column.PropertyName, StringComparison.OrdinalIgnoreCase));
                if (property == null)
                    throw new ImportException($"程序化导入列 '{column.Name}' 引用了类型 '{targetType}' 上不存在的属性 '{column.PropertyName}'");

                // 列名空则回退属性名（与特性声明、强类型选项路径统一；空名列无法匹配表头，静默跳过会丢数据）
                var name = string.IsNullOrEmpty(column.Name) ? property.Name : column.Name;

                // 重复声明防线（特性路径由 AllowMultiple = false 天然防重，程序化路径在此对齐）：
                // 同一属性重复绑定会导致 SetValue 双写与 RawValues 同键覆盖，列名重复会导致表头匹配歧义，均快速失败
                if (!seenProperties.Add(property))
                    throw new ImportException($"程序化导入列 '{name}' 重复绑定属性 '{property.Name}'（同一属性只允许声明一次）");

                if (!seenNames.Add(name))
                    throw new ImportException($"程序化导入列名 '{name}' 重复");

                if (!property.CanWrite)
                    throw new ImportException($"程序化导入列 '{name}' 引用的属性 '{property.Name}' 不可写（缺少 setter）");

                // 程序化列不经特性扫描，非字面量类型且无转换器时快速失败（对齐特性路径校验）
                if (column.Converter == null && !ColumnResolverCore.IsLiteralType(property.PropertyType))
                    throw new ImportException($"程序化导入列 '{name}' 的属性 '{property.Name}' 类型 '{ColumnResolverCore.GetDisplayName(property.PropertyType)}' 不是受支持的字面量类型（请设置 Converter 或改用字面量类型）");

                // 直填且列名非空时直接引用原实例；回查补齐或列名回退时克隆（不改动调用方实例）
                if (property == column.Property && !string.IsNullOrEmpty(column.Name))
                {
                    result.Add(column);
                    continue;
                }

                result.Add(new ImportColumn
                {
                    Name = name,
                    PropertyName = property.Name,
                    Property = property,
                    Format = column.Format,
                    DefaultValue = column.DefaultValue,
                    IsRequired = column.IsRequired,
                    Converter = column.Converter
                });
            }

            return result;
        }

        /// <summary>特性声明的导入列（属性与特性对的扫描产物，绑定列与模板列投影的共同来源）。</summary>
        private sealed class DeclaredImportColumn
        {
            public PropertyInfo Property { get; set; }

            public ImportColumnAttribute Attribute { get; set; }

            /// <summary>解析列名（特性未声明名称时回退属性名）。</summary>
            public string GetColumnName()
            {
                return string.IsNullOrEmpty(Attribute.Name) ? Property.Name : Attribute.Name;
            }
        }
    }
}
