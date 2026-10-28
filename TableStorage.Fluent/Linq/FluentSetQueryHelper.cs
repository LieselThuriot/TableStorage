using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using TableStorage.Fluent;
using TableStorage.Visitors;

namespace TableStorage.Linq;

internal static class FluentSetInfo<TFluent>
    where TFluent : FluentEntityBase
{
    public static readonly FluentDiscriminatorMode Mode = GetMode();

    public static readonly string Discriminator = FluentEntityBase.GetDiscriminatorKey(Mode);

    private static FluentDiscriminatorMode GetMode()
    {
        if (typeof(FluentPartitionTableEntityBase).IsAssignableFrom(typeof(TFluent)))
        {
            return FluentDiscriminatorMode.PartitionKey;
        }

        if (typeof(FluentRowTableEntityBase).IsAssignableFrom(typeof(TFluent)))
        {
            return FluentDiscriminatorMode.RowKey;
        }

        return FluentDiscriminatorMode.DollarType;
    }
}

internal sealed class FluentSetQueryHelper<T, TFluent>(TableSetQueryHelper<TFluent> helper) :
    IAsyncEnumerable<T>,
    ISelectedTableQueryable<T>,
    ITakenTableQueryable<T>,
    IFilteredTableQueryable<T>,
    ISelectedTakenTableQueryable<T>,
    ITableSetQueryHelper<T>
    where T : class, IDictionary<string, object>, ITableEntity, new() // T is one of TFluent's type arguments, validated by FluentQuery
    where TFluent : FluentEntityBase, new()
{
    // The PartitionKey is always the name of type T for FluentPartitionTableEntity, the RowKey for FluentRowTableEntity and the $type field for FluentTableEntity
    private readonly TableSetQueryHelper<TFluent> _helper = helper.AddFilter(CreateFilter(), ModelInfoCache<T>.GetModelInfo(helper));

    private static readonly System.Reflection.PropertyInfo s_partitionKeyProperty = typeof(FluentEntityBase).GetProperty(nameof(FluentEntityBase.PartitionKey))!;
    private static readonly System.Reflection.PropertyInfo s_rowKeyProperty = typeof(FluentEntityBase).GetProperty(nameof(FluentEntityBase.RowKey))!;

    private static Expression<Func<TFluent, bool>> CreateFilter()
    {
        ParameterExpression parameter = Expression.Parameter(typeof(TFluent), "x");

        Expression discriminator = FluentSetInfo<TFluent>.Mode switch
        {
            FluentDiscriminatorMode.PartitionKey => Expression.Property(parameter, s_partitionKeyProperty),
            FluentDiscriminatorMode.RowKey => Expression.Property(parameter, s_rowKeyProperty),
            _ => DollarTypeClass.TypeProperty
        };

        Expression equality = Expression.Equal(discriminator, Expression.Constant(typeof(T).Name));
        return Expression.Lambda<Func<TFluent, bool>>(equality, parameter);
    }

    private static Expression<Func<TFluent, TResult>> AlterExpression<TResult>(Expression<Func<T, TResult>> expression)
    {
        // Swap the parameter type so the expression tree is accepted by the underlying helper.
        // Runtime compilation rewrites property accesses to indexer calls via FluentVisitor.
        ParameterExpression parameter = Expression.Parameter(typeof(TFluent));
        return Expression.Lambda<Func<TFluent, TResult>>(expression.Body, parameter);
    }

    ModelInfo ITableSetQueryHelper.ModelInfo => ModelInfoCache<T>.GetModelInfo(_helper);
    ModelInfo ITableSetQueryHelper.GetModelInfo<TType>() => _helper.Table.GetModelInfo<TType>();
    ModelInfo ITableSetQueryHelper.GetModelInfo(Type type) => _helper.Table.GetModelInfo(type);

    Expression? ITableSetQueryHelper.GetFilter()
    {
        var expression = _helper.GetFilter();

        if (expression is null)
        {
            return null;
        }

        ParameterExpression parameter = Expression.Parameter(typeof(T));
        return Expression.Lambda<Func<T, bool>>(expression.Body, parameter);
    }

    ITableSetQueryHelper<T> ITableSetQueryHelper<T>.SetFields<TResult>(Expression<Func<T, TResult>> exp, bool throwIfNoArgumentsFound)
    {
        SelectFields(exp, throwIfNoArgumentsFound);
        return this;
    }

    ITableSetQueryHelper<T> ITableSetQueryHelper<T>.SetFields(IEnumerable<string> fields)
    {
        _helper.SetFields([FluentSetInfo<TFluent>.Discriminator, .. fields]);
        return this;
    }

    bool ITableSetQueryHelper.HasFields() => _helper.HasFields();

    Task ITableSetQueryHelper.UpdateAsync(ITableEntity entity, CancellationToken cancellationToken) => _helper.Table.UpdateAsync(entity, cancellationToken);

    Task ITableSetQueryHelper.SubmitTransactionAsync(IEnumerable<TableTransactionAction> transactionActions, TransactionSafety transactionSafety, CancellationToken cancellationToken) => _helper.Table.SubmitTransactionAsync(transactionActions, transactionSafety, cancellationToken);

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        await foreach (TFluent entity in _helper.WithCancellation(cancellationToken))
        {
            yield return Unsafe.As<T>(entity.GetRawValue());
        }
    }

    public Task<int> BatchDeleteAsync(CancellationToken token = default) => _helper.BatchDeleteAsync(token);

    public Task<int> BatchDeleteTransactionAsync(CancellationToken token = default) => _helper.BatchDeleteTransactionAsync(token);

    public async Task<T> FirstAsync(CancellationToken token = default) => Unsafe.As<T>((await _helper.FirstAsync(token)).GetRawValue());

    public async Task<T?> FirstOrDefaultAsync(CancellationToken token = default) => Unsafe.As<T>((await _helper.FirstOrDefaultAsync(token))?.GetRawValue());

    public async Task<T> SingleAsync(CancellationToken token = default) => Unsafe.As<T>((await _helper.SingleAsync(token)).GetRawValue());

    public async Task<T?> SingleOrDefaultAsync(CancellationToken token = default) => Unsafe.As<T>((await _helper.SingleOrDefaultAsync(token))?.GetRawValue());

    private void SelectFields<TResult>(Expression<Func<T, TResult>> selector, bool throwIfNoArgumentsFound = true)
    {
        SelectionVisitor visitor = new(ModelInfoCache<T>.GetModelInfo(_helper));
        visitor.Visit(selector);

        if (visitor.Members.Count is 0)
        {
            if (throwIfNoArgumentsFound)
            {
                throw new NotSupportedException("Select expression is not supported");
            }
        }
        else
        {
            visitor.Members.Add(FluentSetInfo<TFluent>.Discriminator);
            _helper.SetFields(visitor.Members);
        }
    }

    ISelectedTableQueryable<T> IFilteredTableQueryable<T>.SelectFields<TResult>(Expression<Func<T, TResult>> selector)
    {
        SelectFields(selector);
        return this;
    }

    ISelectedTakenTableQueryable<T> ITakenTableQueryable<T>.SelectFields<TResult>(Expression<Func<T, TResult>> selector)
    {
        SelectFields(selector);
        return this;
    }

    ITakenTableQueryable<T> IFilteredTableQueryable<T>.Take(int amount)
    {
        _helper.SetAmount(amount);
        return this;
    }

    ISelectedTakenTableQueryable<T> ISelectedTableQueryable<T>.Take(int amount)
    {
        _helper.SetAmount(amount);
        return this;
    }

    IFilteredTableQueryable<T> IFilteredTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
    {
        _helper.AddFilter(AlterExpression(predicate), ModelInfoCache<T>.GetModelInfo(_helper));
        return this;
    }

    ISelectedTakenTableQueryable<T> ISelectedTakenTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
    {
        _helper.AddFilter(AlterExpression(predicate), ModelInfoCache<T>.GetModelInfo(_helper));
        return this;
    }

    ITakenTableQueryable<T> ITakenTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
    {
        _helper.AddFilter(AlterExpression(predicate), ModelInfoCache<T>.GetModelInfo(_helper));
        return this;
    }

    ISelectedTableQueryable<T> ISelectedTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
    {
        _helper.AddFilter(AlterExpression(predicate), ModelInfoCache<T>.GetModelInfo(_helper));
        return this;
    }

    ISelectedTakenTableQueryable<T> ISelectedTakenTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
    {
        _helper.AddExistsInFilter(AlterExpression(predicate), elements, ModelInfoCache<T>.GetModelInfo(_helper));
        return this;
    }

    IFilteredTableQueryable<T> IFilteredTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
    {
        _helper.AddExistsInFilter(AlterExpression(predicate), elements, ModelInfoCache<T>.GetModelInfo(_helper));
        return this;
    }

    ITakenTableQueryable<T> ITakenTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
    {
        _helper.AddExistsInFilter(AlterExpression(predicate), elements, ModelInfoCache<T>.GetModelInfo(_helper));
        return this;
    }

    ISelectedTableQueryable<T> ISelectedTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
    {
        _helper.AddExistsInFilter(AlterExpression(predicate), elements, ModelInfoCache<T>.GetModelInfo(_helper));
        return this;
    }

    ISelectedTakenTableQueryable<T> ISelectedTakenTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
    {
        _helper.AddNotExistsInFilter(AlterExpression(predicate), elements, ModelInfoCache<T>.GetModelInfo(_helper));
        return this;
    }

    IFilteredTableQueryable<T> IFilteredTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
    {
        _helper.AddNotExistsInFilter(AlterExpression(predicate), elements, ModelInfoCache<T>.GetModelInfo(_helper));
        return this;
    }

    ITakenTableQueryable<T> ITakenTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
    {
        _helper.AddNotExistsInFilter(AlterExpression(predicate), elements, ModelInfoCache<T>.GetModelInfo(_helper));
        return this;
    }

    ISelectedTableQueryable<T> ISelectedTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
    {
        _helper.AddNotExistsInFilter(AlterExpression(predicate), elements, ModelInfoCache<T>.GetModelInfo(_helper));
        return this;
    }
}
