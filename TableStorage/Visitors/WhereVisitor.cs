using System.Linq.Expressions;

namespace TableStorage.Visitors;

internal sealed class WhereVisitor(ModelInfo modelInfo) : ExpressionVisitor
{
    private readonly string? _partitionKeyProxy = modelInfo.PartitionKey;
    private readonly string? _rowKeyProxy = modelInfo.RowKey;
    private readonly Type _entityType = modelInfo.EntityType;

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression is ParameterExpression parameter)
        {
            if (parameter.Type == _entityType)
            {
                string name = node.Member.Name;

                if (name == _partitionKeyProxy && name is not nameof(ITableEntity.PartitionKey))
                {
                    return Expression.Property(Expression.Convert(parameter, typeof(ITableEntity)), TableEntityMembers.PartitionKey);
                }

                if (name == _rowKeyProxy && name is not nameof(ITableEntity.RowKey))
                {
                    return Expression.Property(Expression.Convert(parameter, typeof(ITableEntity)), TableEntityMembers.RowKey);
                }
            }

            return node;
        }

        Expression? instance = Visit(node.Expression);

        if (ExpressionEvaluator.TryEvaluateMember(node, instance, out ConstantExpression constant))
        {
            return constant;
        }

        return node.Update(instance);
    }
}
