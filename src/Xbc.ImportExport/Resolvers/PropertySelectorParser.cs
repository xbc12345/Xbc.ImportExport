using System.Linq.Expressions;
using System.Reflection;

namespace Xbc.ImportExport
{
    /// <summary>属性选择器解析器（提取一级属性元数据，供导入/导出选项共用）。</summary>
    internal static class PropertySelectorParser
    {
        /// <summary>解析属性选择器的属性元数据（须为实体一级属性访问，如 x => x.Code）。</summary>
        /// <exception cref="ArgumentException">选择器不是实体一级属性访问（嵌套成员、静态成员、字段、方法调用等）。</exception>
        public static PropertyInfo GetProperty<T, TValue>(Expression<Func<T, TValue>> selector)
        {
            var member = TryParseMemberExpression(selector.Body);
            if (member == null
                || member.Member is not PropertyInfo property
                || member.Expression is not ParameterExpression)
            {
                throw new ArgumentException("selector 必须为形如 'x => x.属性名' 的属性访问表达式", nameof(selector));
            }

            return property;
        }

        /// <summary>迭代解包类型转换节点（Convert/ConvertChecked），提取成员访问表达式（非成员访问返回 null；其余一元节点如负号不解包，交由上层按非属性访问拒绝）。</summary>
        private static MemberExpression TryParseMemberExpression(Expression expression)
        {
            while (expression is UnaryExpression unary
                && (unary.NodeType == ExpressionType.Convert
                    || unary.NodeType == ExpressionType.ConvertChecked))
            {
                expression = unary.Operand;
            }

            return expression as MemberExpression;
        }
    }
}
