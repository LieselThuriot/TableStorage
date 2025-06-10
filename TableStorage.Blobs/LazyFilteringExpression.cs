using FastExpressionCompiler;
using System.Linq.Expressions;

namespace TableStorage;

internal sealed class LazyFilteringExpression<T>(Expression<Func<T, bool>> expression) : Lazy<Func<T, bool>>(() => expression.CompileFast())
{
    public bool Invoke(T entity) => Value(entity);

    public static implicit operator LazyFilteringExpression<T>(Expression<Func<T, bool>> expression) => new(expression);
}