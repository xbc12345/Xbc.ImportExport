using System.Collections.Concurrent;
using System.Reflection;

namespace Xbc.ImportExport
{
    /// <summary>列解析共享内核（属性扫描、排序键与转换器激活，供导入/导出列解析器共用）。</summary>
    internal static class ColumnResolverCore
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> propertyCache
            = new ConcurrentDictionary<Type, PropertyInfo[]>();

        /// <summary>获取公共实例属性（排除索引器，按类型缓存，列定义解析与属性绑定的统一扫描入口）。</summary>
        internal static PropertyInfo[] GetInstanceProperties(Type targetType)
        {
            return propertyCache.GetOrAdd(targetType, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0)
                .ToArray());
        }

        /// <summary>按 Order 升序排序（显式正 Order 在前且按 Order 升序，Order = 0 或负值按声明顺序排后，导入/导出列序的统一规则）。</summary>
        internal static List<T> SortByOrder<T>(List<T> items, Func<T, int> orderSelector)
        {
            return items
                .Select((item, idx) => new
                {
                    Item = item,
                    Order = orderSelector(item),
                    Index = idx
                })
                .OrderBy(x => x.Order > 0 ? 0 : 1)
                .ThenBy(x => x.Order > 0 ? x.Order : x.Index)
                .Select(x => x.Item)
                .ToList();
        }

        /// <summary>判断是否为受支持的字面量类型（字符串、数值、布尔、日期时间、Guid、枚举及可空包装，集合/字典/复合类型须声明转换器）。</summary>
        internal static bool IsLiteralType(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            return underlying == typeof(string)
                || underlying == typeof(int) || underlying == typeof(uint)
                || underlying == typeof(long) || underlying == typeof(ulong)
                || underlying == typeof(short) || underlying == typeof(ushort)
                || underlying == typeof(byte) || underlying == typeof(sbyte)
                || underlying == typeof(double) || underlying == typeof(float) || underlying == typeof(decimal)
                || underlying == typeof(bool)
                || underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset)
                || underlying == typeof(TimeSpan)
                || underlying == typeof(Guid)
                || underlying.IsEnum;
        }

        /// <summary>获取类型显示名（可空类型按底层类型，避免消息显示 Nullable`1）。</summary>
        internal static string GetDisplayName(Type type)
        {
            return (Nullable.GetUnderlyingType(type) ?? type).ToString();
        }

        /// <summary>校验并实例化特性声明的转换器（须实现指定接口且有无参构造，失败抛域内异常）。</summary>
        internal static object CreateConverterInstance(
            Type converterType,
            Type targetType,
            Type requiredInterface,
            Func<string, ImportExportException> errorFactory)
        {
            if (!requiredInterface.IsAssignableFrom(converterType))
                throw errorFactory($"类型 '{targetType}' 的转换器 '{converterType}' 未实现 '{requiredInterface}' 接口");

            if (converterType.GetConstructor(Type.EmptyTypes) == null)
                throw errorFactory($"类型 '{targetType}' 的转换器 '{converterType}' 未定义无参构造函数");

            return Activator.CreateInstance(converterType);
        }
    }
}
