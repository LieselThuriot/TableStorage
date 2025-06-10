using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using TableStorage.Visitors;

namespace TableStorage;

internal readonly struct CompiledBlobQueryHandler<T, TClient>(BaseBlobSet<T, TClient> blobset, LazyAsync<BlobContainerClient> containerClient)
    where TClient : BlobBaseClient
    where T : IBlobEntity
{
    private readonly BaseBlobSet<T, TClient> _blobset = blobset;
    private readonly LazyAsync<BlobContainerClient> _containerClient = containerClient;

    public IAsyncEnumerable<(TClient client, LazyAsync<T?> entity)> QueryAsync(Expression<Func<T, bool>>? filter, CancellationToken cancellationToken = default)
    {
        if (filter is null)
        {
            return _blobset.IterateAllBlobs(cancellationToken);
        }

        BlobQueryVisitor visitor = new(_blobset.PartitionKeyProxy, _blobset.RowKeyProxy, _blobset.Tags);
        Expression<Func<T, bool>> visitedFilter = visitor.VisitAndConvert(filter, nameof(QueryAsync));

        if (!visitor.Error)
        {
            if (_blobset.Options.UseTags)
            {
                if (visitor.SimpleFilter)
                {
                    return _blobset.IterateBlobsByTag(visitor.Filter!, cancellationToken);
                }

                return IterateBlobsByTagAndComplexFilter(visitor.Filter!, filter, cancellationToken);
            }
        }

        if (visitor.OperandError && _blobset.Options.UseTags)
        {
            return IterateFilteredByTagsAtRuntime(filter, visitor.TagOnlyFilter, cancellationToken);
        }

        return IterateFilteredAtRuntime(filter, cancellationToken);
    }

    private async IAsyncEnumerable<(TClient client, LazyAsync<T?> entity)> IterateBlobsByTagAndComplexFilter(string filter, LazyFilteringExpression<T> compiledFilter, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach ((TClient client, LazyAsync<T?> entity) result in _blobset.IterateBlobsByTag(filter, cancellationToken))
        {
            T? entity = await result.entity;
            if (entity is not null && compiledFilter.Invoke(entity))
            {
                yield return result;
            }
        }
    }

    private async IAsyncEnumerable<(TClient client, LazyAsync<T?> entity)> IterateFilteredAtRuntime(LazyFilteringExpression<T> filter, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach ((TClient client, LazyAsync<T?> entity) result in _blobset.IterateAllBlobs(cancellationToken))
        {
            T? entity = await result.entity;
            if (entity is not null && filter.Invoke(entity))
            {
                yield return result;
            }
        }
    }

    private async IAsyncEnumerable<(TClient client, LazyAsync<T?> entity)> IterateFilteredByTagsAtRuntime(Expression<Func<T, bool>> filter, bool tagOnlyFilter, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!_blobset.Options.UseTags)
        {
            throw new InvalidOperationException("Tags is disabled yet we ended up in a tags call");
        }

        LazyFilteringExpression<T> originalCompiledFilter = filter;

        BlobTagQueryVisitor<T> visitor = new(_blobset.PartitionKeyProxy, _blobset.RowKeyProxy, _blobset.Tags);
        var visitedFilter = (Expression<Func<BlobTagAccessor, bool>>)visitor.Visit(filter);
        LazyFilteringExpression<BlobTagAccessor> compiledFilter = visitedFilter;

        BlobContainerClient container = await _containerClient;
        await foreach (BlobItem blob in container.GetBlobsAsync(BlobTraits.Tags, BlobStates.None, cancellationToken: cancellationToken))
        {
            BlobTagAccessor tags = new(blob.Tags);

            if (compiledFilter.Invoke(tags))
            {
                TClient client = _blobset.GetClient(container, blob.Name);
                BaseBlobSet<T, TClient> set = _blobset;
                LazyAsync<T?> entity = new(() => set.Download(client, cancellationToken));

                if (!tagOnlyFilter)
                {
                    var entityResult = await entity;
                    if (entityResult is null || !originalCompiledFilter.Invoke(entityResult))
                    {
                        continue;
                    }
                }

                yield return (client, entity);
            }
        }
    }
}
