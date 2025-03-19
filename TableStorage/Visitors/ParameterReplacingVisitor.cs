using System.Linq.Expressions;

namespace TableStorage.Visitors;

internal sealed class ParameterReplacingVisitor(ParameterExpression oldParameter, ParameterExpression newParameter) : ExpressionVisitor
{
    private readonly ParameterExpression _oldParameter = oldParameter;
    private readonly ParameterExpression _newParameter = newParameter;

    protected override Expression VisitParameter(ParameterExpression node) => node == _oldParameter ? _newParameter : base.VisitParameter(node);
}