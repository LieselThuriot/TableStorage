using System.Linq.Expressions;
using System.Reflection;

namespace TableStorage.Visitors;

internal sealed class BlobTagAccessor(IDictionary<string, string> dictionary)
{
    private readonly IDictionary<string, string> _dictionary = dictionary;

    public string? Get(string key) => _dictionary.TryGetValue(key, out string? value) ? value : null;

    public static readonly MethodInfo MethodInfo = typeof(BlobTagAccessor).GetMethod(nameof(Get))!;
}

internal sealed class BlobTagQueryVisitor<T>(string? partitionKeyProxy, string? rowKeyProxy) : ExpressionVisitor
    where T : IBlobEntity
{
    private readonly string? _partitionKeyProxy = partitionKeyProxy;
    private readonly string? _rowKeyProxy = rowKeyProxy;
    private readonly Dictionary<ParameterExpression, ParameterExpression> _parameterMap = [];

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Type == typeof(string))
        {
            return node;
        }

        return Expression.Constant(node.Value?.ToString());
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (node.Type != typeof(T))
        {
            return node;
        }

        if (!_parameterMap.TryGetValue(node, out ParameterExpression? parameter))
        {
            _parameterMap[node] = parameter = Expression.Parameter(typeof(BlobTagAccessor), node.Name);
        }

        return parameter;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        string name = node.Member.Name;
        if (name == _partitionKeyProxy)
        {
            name = "partition";
        }
        else if (name == _rowKeyProxy)
        {
            name = "row";
        }

        var expression = Visit(node.Expression);
        return Expression.Call(expression, BlobTagAccessor.MethodInfo, Expression.Constant(name));
    }

    protected override Expression VisitInvocation(InvocationExpression node)
    {
        if (node.Expression is LambdaExpression lambdaExpression)
        {
            var parameters = lambdaExpression.Parameters.Select(Visit).Cast<ParameterExpression>().ToList();
            var arguments = node.Arguments.Select(Visit).ToList();

            var body = Visit(lambdaExpression.Body);
            var lambda = Expression.Lambda(body, parameters);

            node = Expression.Invoke(lambda, arguments);
        }

        return base.VisitInvocation(node);
    }

    protected override Expression VisitLambda<Tl>(Expression<Tl> node)
    {
        var parameters = node.Parameters.Select(Visit).Cast<ParameterExpression>().ToList();
        var body = Visit(node.Body);
        return Expression.Lambda(body, parameters);
    }
}
