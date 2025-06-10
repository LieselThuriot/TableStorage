using Azure.Storage.Blobs.Specialized;
using System.Linq.Expressions;
using TableStorage.Visitors;

namespace TableStorage.Linq;

internal sealed class BlobSetQueryHelper<T, TClient>(BaseBlobSet<T, TClient> table) :
    IAsyncEnumerable<T>,
    IFilteredBlobQueryable<T>
    where T : IBlobEntity
    where TClient : BlobBaseClient
{
    private ParameterExpression? _parameter;

    internal BaseBlobSet<T, TClient> Table { get; } = table;
    private Expression<Func<T, bool>>? _filter;

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (_filter is null)
        {
            return Table.GetAsyncEnumerator(cancellationToken);
        }

        return Iterate();
        async IAsyncEnumerator<T> Iterate()
        {
            await foreach ((TClient _, LazyAsync<T?> result) in Table.QueryInternalAsync(_filter, cancellationToken))
            {
                T? entity = await result;

                if (entity is not null)
                {
                    yield return entity;
                }
            }
        }
    }

    public async Task<int> BatchDeleteAsync(CancellationToken token = default)
    {
        int count = 0;

        await foreach ((TClient client, LazyAsync<T?> _) in Table.QueryInternalAsync(_filter, token))
        {
            await client.DeleteIfExistsAsync(cancellationToken: token);
            count++;
        }

        return count;
    }

    public IFilteredBlobQueryable<T> ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
        => Where(predicate.CreateExistsInFilter(elements));

    public IFilteredBlobQueryable<T> NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
        => Where(predicate.CreateNotExistsInFilter(elements));

    public Task<T> FirstAsync(CancellationToken token)
        => Helpers.FirstAsync(this, token);

    public Task<T?> FirstOrDefaultAsync(CancellationToken token)
        => Helpers.FirstOrDefaultAsync(this, token);

    public Task<T> SingleAsync(CancellationToken token = default)
        => Helpers.SingleAsync(this, token);

    public Task<T?> SingleOrDefaultAsync(CancellationToken token = default)
        => Helpers.SingleOrDefaultAsync(this, token);

    public IFilteredBlobQueryable<T> Where(Expression<Func<T, bool>> predicate)
    {
        if (_filter is null)
        {
            _filter = predicate;
            _parameter = predicate.Parameters[0];
        }
        else
        {
            ParameterReplacingVisitor predicateVisitor = new(predicate.Parameters[0], _parameter!);
            predicate = predicateVisitor.VisitAndConvert(predicate, nameof(Where));

            _filter = Expression.Lambda<Func<T, bool>>(Expression.AndAlso(_filter.Body, predicate.Body), _parameter);
        }

        return this;
    }
}