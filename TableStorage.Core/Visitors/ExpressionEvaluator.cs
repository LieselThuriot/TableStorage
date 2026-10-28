using System.Linq.Expressions;
using System.Reflection;

namespace TableStorage.Visitors;

internal static class ExpressionEvaluator
{
    /// <summary>
    /// Evaluates a member access that does not depend on a lambda parameter, such as a static member,
    /// a captured local or a chain of those (e.g. <c>closure.settings.Value</c>).
    /// </summary>
    /// <param name="node">The member access to evaluate.</param>
    /// <param name="instance">The already visited instance expression of <paramref name="node"/>, or <c>null</c> for static members.</param>
    /// <param name="result">The evaluated value, typed as the member type.</param>
    public static bool TryEvaluateMember(MemberExpression node, Expression? instance, out ConstantExpression result)
    {
        object? container = null;

        if (instance is not null)
        {
            if (instance is not ConstantExpression constant || constant.Value is null)
            {
                result = null!;
                return false;
            }

            container = constant.Value;
        }

        switch (node.Member)
        {
            case FieldInfo field:
                result = Expression.Constant(field.GetValue(container), node.Type);
                return true;

            case PropertyInfo property:
                result = Expression.Constant(property.GetValue(container), node.Type);
                return true;

            default:
                result = null!;
                return false;
        }
    }

    /// <summary>
    /// Evaluates a member access chain that does not depend on a lambda parameter.
    /// </summary>
    public static bool TryEvaluateMember(MemberExpression node, out ConstantExpression result)
    {
        Expression? instance = node.Expression;

        if (instance is MemberExpression inner)
        {
            if (!TryEvaluateMember(inner, out ConstantExpression innerResult))
            {
                result = null!;
                return false;
            }

            instance = innerResult;
        }

        return TryEvaluateMember(node, instance, out result);
    }

    /// <summary>
    /// Gets the value of a constant or of a member access chain that does not depend on a lambda parameter.
    /// </summary>
    public static bool TryGetValue(Expression expression, out object? value)
    {
        switch (expression)
        {
            case ConstantExpression constant:
                value = constant.Value;
                return true;

            case MemberExpression member when TryEvaluateMember(member, out ConstantExpression result):
                value = result.Value;
                return true;

            default:
                value = null;
                return false;
        }
    }
}
