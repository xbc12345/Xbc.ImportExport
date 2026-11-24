using System.Globalization;

namespace Xbc.ImportExport
{
    /// <summary>导入值解析器（字符串到目标类型的宽容解析，失败可观测，按错误模式分派，Csv/Excel 导入器共用）。</summary>
    internal static class ImportValueParser
    {
        /// <summary>按错误模式解析列值（严格抛数据异常定位行列；收集记录错误；宽容与收集失败均返回 null，导入器跳过赋值即保持属性默认值）。</summary>
        public static object Resolve(
            ImportColumnMapping mapping,
            string value,
            ImportErrorMode errorMode,
            int rowNumber,
            List<string> errors)
        {
            // 空单元格：必填缺失（未声明默认值）按错误模式分派，否则应用声明默认值（无默认值或解析失败返回 null，跳过赋值实体保持属性默认值）
            if (string.IsNullOrEmpty(value))
            {
                if (mapping.Column.IsRequired && mapping.Column.DefaultValue == null)
                {
                    return OnFailure(
                        $"列 '{mapping.Column.Name}' 为必填列，单元格为空且未声明默认值",
                        rowNumber, mapping.Column.Name, errorMode, errors, null);
                }

                return ResolveDeclaredDefault(mapping);
            }

            // 声明式转换器优先；异常与返回值类型不兼容均按错误模式分派（严格抛数据异常 / 收集记录 / 宽容静默回退 null）
            if (mapping.Column.Converter != null)
            {
                object converted;
                try
                {
                    converted = mapping.Column.Converter(value);
                }
                catch (Exception ex)
                {
                    return OnFailure(
                        $"列 '{mapping.Column.Name}' 的值 '{value}' 转换器执行失败：{ex.Message}",
                        rowNumber, mapping.Column.Name, errorMode, errors, ex);
                }

                // 类型守卫：返回值必须与属性类型兼容（null 放行 = 跳过赋值语义）。
                // 不兼容值直送 SetValue 抛裸 ArgumentException，被导入器外层包装成无列名异常，定位全丢；
                // 借鉴 DbAccess EntityValueConverter 的转换守卫，在此统一携带行号/列名/双类型按错误模式分派
                var underlying = Nullable.GetUnderlyingType(mapping.Column.Property.PropertyType)
                                 ?? mapping.Column.Property.PropertyType;
                if (converted != null && !underlying.IsInstanceOfType(converted))
                {
                    return OnFailure(
                        $"列 '{mapping.Column.Name}' 的转换器返回了 '{converted.GetType()}'（值 '{converted}'），无法赋给 '{ColumnResolverCore.GetDisplayName(mapping.Column.Property.PropertyType)}' 属性",
                        rowNumber, mapping.Column.Name, errorMode, errors, null);
                }

                return converted;
            }

            if (TryParse(value, mapping.Column.Property.PropertyType, mapping.Column.Format, out var result))
                return result;

            return OnFailure(
                $"列 '{mapping.Column.Name}' 的值 '{value}' 无法解析为 '{ColumnResolverCore.GetDisplayName(mapping.Column.Property.PropertyType)}' 类型",
                rowNumber, mapping.Column.Name, errorMode, errors, null);
        }

        /// <summary>解析声明默认值（类型兼容的装箱值直用；字符串声明按解析路径；不兼容转文本解析；失败返回 null 走类型默认值）。</summary>
        private static object ResolveDeclaredDefault(ImportColumnMapping mapping)
        {
            var declared = mapping.Column.DefaultValue;
            if (declared == null)
                return null;

            var propertyType = mapping.Column.Property.PropertyType;
            var underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            // 声明类型与目标兼容（含可空解包，装箱可空值与基础类型运行时等价）直用
            if (underlying.IsInstanceOfType(declared))
                return declared;

            // 字符串声明按解析路径（空串视为未声明）
            if (declared is string text)
            {
                if (string.IsNullOrEmpty(text))
                    return null;

                return TryParse(text, propertyType, mapping.Column.Format, out var result)
                    ? result
                    : null;
            }

            // 类型不兼容（如数值声明给字符串属性）转文本按解析路径
            text = Convert.ToString(declared, CultureInfo.InvariantCulture);
            return TryParse(text, propertyType, mapping.Column.Format, out var converted)
                ? converted
                : null;
        }

        /// <summary>失败处理（严格模式抛数据异常定位到行与列；收集模式记录错误；否则静默返回 null）。</summary>
        private static object OnFailure(
            string message,
            int rowNumber,
            string columnName,
            ImportErrorMode errorMode,
            List<string> errors,
            Exception innerException)
        {
            if (errorMode == ImportErrorMode.Strict)
                throw new ImportDataException(message, rowNumber, columnName, innerException);

            if (errorMode == ImportErrorMode.Collect)
                errors?.Add(message);

            return null;
        }

        /// <summary>尝试解析值（失败返回 false 且 result 为 null，由调用方按错误模式分派或回退 null；空值防御性视为成功，白名单外类型按失败处理）。</summary>
        private static bool TryParse(string value, Type targetType, string format, out object result)
        {
            // 失败与空值的 result 统一为 null（导入器按 null 跳过 SetValue，实体保持 new T() 的属性默认值）
            result = null;
            if (string.IsNullOrEmpty(value))
                return true;

            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (underlying == typeof(string))
            {
                result = value;
                return true;
            }

            if (underlying == typeof(int) || underlying == typeof(short) || underlying == typeof(byte) || underlying == typeof(sbyte))
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                    return TryChangeType(i, underlying, out result);

                return false;
            }

            if (underlying == typeof(ushort) || underlying == typeof(uint) || underlying == typeof(ulong))
            {
                // 无符号样式天然拒负数，窄化溢出按解析失败处理
                if (ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var u))
                    return TryChangeType(u, underlying, out result);

                return false;
            }

            if (underlying == typeof(long))
            {
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                {
                    result = l;
                    return true;
                }

                return false;
            }

            if (underlying == typeof(double))
            {
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                {
                    result = d;
                    return true;
                }

                return false;
            }

            if (underlying == typeof(float))
            {
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                {
                    result = f;
                    return true;
                }

                return false;
            }

            if (underlying == typeof(decimal))
            {
                // decimal 直接解析，不经 double 中转（double 仅 15-17 位精度，会截断 28 位 decimal）
                if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var m))
                {
                    result = m;
                    return true;
                }

                return false;
            }

            if (underlying == typeof(bool))
            {
                if (TryParseBoolean(value, out var b))
                {
                    result = b;
                    return true;
                }

                return false;
            }

            if (underlying == typeof(DateTime))
            {
                if (TryParseDateTime(value, format, out var dt))
                {
                    result = dt;
                    return true;
                }

                return false;
            }

            if (underlying == typeof(DateTimeOffset))
            {
                if (TryParseDateTimeOffset(value, format, out var dto))
                {
                    result = dto;
                    return true;
                }

                return false;
            }

            if (underlying == typeof(TimeSpan))
            {
                if (TryParseTimeSpan(value, format, out var ts))
                {
                    result = ts;
                    return true;
                }

                return false;
            }

            if (underlying == typeof(Guid))
            {
                if (Guid.TryParse(value, out var g))
                {
                    result = g;
                    return true;
                }

                return false;
            }

            if (underlying.IsEnum)
            {
                // 名称匹配（大小写不敏感）或数值解析，且必须落在已定义值集合内（拦截越界数值）；
                // [Flags] 枚举按标准解析语义放行组合值与位掩码数值（名称有效性由 Enum.Parse 校验）
                try
                {
                    var parsed = Enum.Parse(underlying, value, true);
                    if (Enum.IsDefined(underlying, parsed)
                        || underlying.IsDefined(typeof(FlagsAttribute), false))
                    {
                        result = parsed;
                        return true;
                    }
                }
                catch { }

                return false;
            }

            // 白名单外类型（仅转换器列的默认值路径可达）解析失败，由调用方回退默认值路径
            return false;
        }

        /// <summary>数值窄化转换（超出目标类型范围按解析失败处理，不冒异常，走错误模式分派）。</summary>
        private static bool TryChangeType(object value, Type targetType, out object result)
        {
            try
            {
                result = Convert.ChangeType(value, targetType);
                return true;
            }
            catch (OverflowException)
            {
                result = null;
                return false;
            }
        }

        private static bool TryParseBoolean(string value, out bool result)
        {
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "是", StringComparison.OrdinalIgnoreCase))
            {
                result = true;
                return true;
            }

            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "no", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "否", StringComparison.OrdinalIgnoreCase))
            {
                result = false;
                return true;
            }

            result = false;
            return false;
        }

        private static bool TryParseDateTime(string value, string format, out DateTime result)
        {
            if (string.IsNullOrEmpty(format))
                return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);

            return DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
        }

        private static bool TryParseDateTimeOffset(string value, string format, out DateTimeOffset result)
        {
            if (string.IsNullOrEmpty(format))
                return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);

            return DateTimeOffset.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
        }

        private static bool TryParseTimeSpan(string value, string format, out TimeSpan result)
        {
            if (string.IsNullOrEmpty(format))
                return TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out result);

            return TimeSpan.TryParseExact(value, format, CultureInfo.InvariantCulture, out result);
        }
    }
}
