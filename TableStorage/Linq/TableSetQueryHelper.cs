﻿using System.Linq.Expressions;
using TableStorage.Visitors;

namespace TableStorage.Linq;

internal sealed class TableSetQueryHelper<T>(TableSet<T> table) :
    IAsyncEnumerable<T>,
    ISelectedTableQueryable<T>,
    ITakenTableQueryable<T>,
    IFilteredTableQueryable<T>,
    ISelectedTakenTableQueryable<T>
    where T : class, ITableEntity, new()
{
    private ParameterExpression? _parameter;

    internal TableSet<T> Table { get; } = table;

    private HashSet<string>? _fields;
    private Expression<Func<T, bool>>? _filter;
    private int? _amount;

    public Task<T> FirstAsync(CancellationToken token)
    {
        _amount = 1;
        return Helpers.FirstAsync(this, token);
    }

    public Task<T?> FirstOrDefaultAsync(CancellationToken token)
    {
        _amount = 1;
        return Helpers.FirstOrDefaultAsync(this, token);
    }

    public Task<T> SingleAsync(CancellationToken token = default)
    {
        return Helpers.SingleAsync(this, token);
    }

    public Task<T?> SingleOrDefaultAsync(CancellationToken token = default)
    {
        return Helpers.SingleOrDefaultAsync(this, token);
    }

    public async Task<int> BatchDeleteAsync(CancellationToken token)
    {
        _fields = [nameof(ITableEntity.PartitionKey), nameof(ITableEntity.RowKey)];

        await using IAsyncEnumerator<T> enumerator = GetAsyncEnumerator(token);

        int result = 0;

        while (await enumerator.MoveNextAsync())
        {
            T current = enumerator.Current;
            await Table.DeleteEntityAsync(current.PartitionKey, current.RowKey, current.ETag, token);
            result++;
        }

        return result;
    }

    public async Task<int> BatchDeleteTransactionAsync(CancellationToken token)
    {
        _fields = [nameof(ITableEntity.PartitionKey), nameof(ITableEntity.RowKey)];

        List<TableTransactionAction> entities = [];

        await using IAsyncEnumerator<T> enumerator = GetAsyncEnumerator(token);

        while (await enumerator.MoveNextAsync())
        {
            T current = enumerator.Current;
            entities.Add(new(TableTransactionActionType.Delete, current, current.ETag));
        }

        await Table.SubmitTransactionAsync(entities, TransactionSafety.Enabled, token);
        return entities.Count;
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        IAsyncEnumerable<T> query = _filter is null
                    ? Table.QueryAsync((string?)null, _amount, _fields, cancellationToken)
                    : Table.QueryAsync(_filter, _amount, _fields, cancellationToken);

        if (_amount.HasValue)
        {
            query = IterateWithAmount(query);
        }

        return query.GetAsyncEnumerator(cancellationToken);

        async IAsyncEnumerable<T> IterateWithAmount(IAsyncEnumerable<T> values)
        {
            int count = _amount.GetValueOrDefault();
            await foreach (T item in values)
            {
                yield return item;

                if (--count == 0)
                {
                    yield break;
                }
            }
        }
    }

    #region Select
    internal bool HasFields() => _fields is not null;

    internal TableSetQueryHelper<T> SetFields(IEnumerable<string> fields)
    {
        if (_fields is not null)
        {
            throw new NotSupportedException("Only one transformation is allowed at a time");
        }

        _fields = [.. fields];

        return this;
    }

    internal TableSetQueryHelper<T> SetFields<TResult>(ref Expression<Func<T, TResult>> exp, bool throwIfNoArgumentsFound = true)
    {
        if (_fields is not null)
        {
            throw new NotSupportedException("Only one transformation is allowed at a time");
        }

        SelectionVisitor visitor = new(Table.PartitionKeyProxy, Table.RowKeyProxy);
        exp = (Expression<Func<T, TResult>>)visitor.Visit(exp);

        if (visitor.Members.Count == 0)
        {
            if (throwIfNoArgumentsFound)
            {
                throw new NotSupportedException("Select expression is not supported");
            }
        }
        else
        {
            _fields = visitor.Members;
        }

        return this;
    }

    ISelectedTakenTableQueryable<T> ITakenTableQueryable<T>.SelectFields<TResult>(Expression<Func<T, TResult>> selector) => SetFields(ref selector);

    ISelectedTableQueryable<T> IFilteredTableQueryable<T>.SelectFields<TResult>(Expression<Func<T, TResult>> selector) => SetFields(ref selector);

    #endregion Select

    #region Take
    internal TableSetQueryHelper<T> SetAmount(int amount)
    {
        if (amount < 1)
        {
            throw new InvalidOperationException("Amount must be a strictly postive integer.");
        }

        _amount = amount;
        return this;
    }

    ISelectedTakenTableQueryable<T> ISelectedTableQueryable<T>.Take(int amount) => SetAmount(amount);

    ITakenTableQueryable<T> IFilteredTableQueryable<T>.Take(int amount) => SetAmount(amount);
    #endregion Take

    #region Where
    internal TableSetQueryHelper<T> AddFilter(Expression<Func<T, bool>> predicate)
    {
        if (Table.PartitionKeyProxy is not null || Table.RowKeyProxy is not null)
        {
            WhereVisitor visitor = new(Table.PartitionKeyProxy, Table.RowKeyProxy, Table.Type);
            predicate = (Expression<Func<T, bool>>)visitor.Visit(predicate);
        }

        if (_filter is null)
        {
            _filter = predicate;
            _parameter = predicate.Parameters[0];
        }
        else
        {
            ParameterReplacingVisitor predicateVisitor = new(predicate.Parameters[0], _parameter!);
            predicate = predicateVisitor.VisitAndConvert(predicate, nameof(AddFilter));
            _filter = Expression.Lambda<Func<T, bool>>(Expression.AndAlso(_filter.Body, predicate.Body), _parameter);
        }

        return this;
    }

    ISelectedTableQueryable<T> ISelectedTableQueryable<T>.Where(Expression<Func<T, bool>> predicate) => AddFilter(predicate);

    ITakenTableQueryable<T> ITakenTableQueryable<T>.Where(Expression<Func<T, bool>> predicate) => AddFilter(predicate);

    IFilteredTableQueryable<T> IFilteredTableQueryable<T>.Where(Expression<Func<T, bool>> predicate) => AddFilter(predicate);

    ISelectedTakenTableQueryable<T> ISelectedTakenTableQueryable<T>.Where(Expression<Func<T, bool>> predicate) => AddFilter(predicate);
    #endregion Where

    #region ExistsIn
    internal TableSetQueryHelper<T> AddExistsInFilter<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
    {
        Expression<Func<T, bool>> lambda = predicate.CreateExistsInFilter(elements);
        return AddFilter(lambda);
    }

    ISelectedTableQueryable<T> ISelectedTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddExistsInFilter(predicate, elements);

    ITakenTableQueryable<T> ITakenTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddExistsInFilter(predicate, elements);

    IFilteredTableQueryable<T> IFilteredTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddExistsInFilter(predicate, elements);

    ISelectedTakenTableQueryable<T> ISelectedTakenTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddExistsInFilter(predicate, elements);
    #endregion ExistsIn

    #region NotExistsIn
    internal TableSetQueryHelper<T> AddNotExistsInFilter<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
    {
        Expression<Func<T, bool>> lambda = predicate.CreateNotExistsInFilter(elements);
        return AddFilter(lambda);
    }

    ISelectedTableQueryable<T> ISelectedTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddNotExistsInFilter(predicate, elements);

    ITakenTableQueryable<T> ITakenTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddNotExistsInFilter(predicate, elements);

    IFilteredTableQueryable<T> IFilteredTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddNotExistsInFilter(predicate, elements);

    ISelectedTakenTableQueryable<T> ISelectedTakenTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddNotExistsInFilter(predicate, elements);
    #endregion NotExistsIn
}