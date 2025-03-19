using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using TableStorage.Visitors;

namespace TableStorage;

public readonly struct BlobId(string partitionKey, string rowKey)
{
    public string PartitionKey { get; } = partitionKey;
    public string RowKey { get; } = rowKey;

    internal static string Get(string partitionKey, string rowKey) =>
            $"{partitionKey ?? throw new ArgumentNullException(nameof(partitionKey))}/{rowKey ?? throw new ArgumentNullException(nameof(rowKey))}";

    internal static BlobId Parse(string id)
    {
        int lastSlash = id.LastIndexOf('/') + 1;
        string partitionKey = id.Substring(0, lastSlash - 1);
        string rowKey = id.Substring(lastSlash);
        return new(partitionKey, rowKey);
    }

    internal static BlobId Get(BlobBaseClient client) => Parse(client.Name);
}

public abstract class BaseBlobSet<T, TClient> : IStorageSet<T>
    where TClient : BlobBaseClient
    where T : IBlobEntity
{
    protected const string PartitionTagConstant = "partition";
    protected const string RowTagConstant = "row";

    public string Name { get; }
    public Type Type => typeof(T);
    public string EntityType => Type.Name;

    protected readonly BlobOptions _options;
    protected readonly string? _partitionKeyProxy;
    protected readonly string? _rowKeyProxy;
    protected readonly IReadOnlyCollection<string> _tags;
    private readonly LazyAsync<BlobContainerClient> _containerClient;

    internal BaseBlobSet(BlobStorageFactory factory, string tableName, BlobOptions options, string? partitionKeyProxy, string? rowKeyProxy, IReadOnlyCollection<string> tags)
    {
        Name = tableName;
        _options = options;
        _partitionKeyProxy = partitionKeyProxy;
        _rowKeyProxy = rowKeyProxy;
        _tags = tags;
        _containerClient = new(() => factory.GetClient(tableName));
    }

    protected Task<TClient> GetClient(IBlobEntity entity)
    {
        return GetClient(entity.PartitionKey, entity.RowKey);
    }

    protected async Task<TClient> GetClient(string partitionKey, string rowKey)
    {
        string id = BlobId.Get(partitionKey, rowKey);
        BlobContainerClient client = await _containerClient;
        return GetClient(client, id);
    }

    protected abstract TClient GetClient(BlobContainerClient containerClient, string id);

    public async Task<T?> GetEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        TClient blob = await GetClient(partitionKey, rowKey);

        if (!await blob.ExistsAsync(cancellationToken))
        {
            throw new KeyNotFoundException($"Entity with PartitionKey '{partitionKey}' and RowKey = '{rowKey}' was not found.");
        }

        return await Download(blob, cancellationToken);
    }

    public async Task<T?> GetEntityOrDefaultAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        (bool _, T? result) = await TryGetEntityAsync(partitionKey, rowKey, cancellationToken);
        return result;
    }

    public async Task<(bool success, T? entity)> TryGetEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        TClient blob = await GetClient(partitionKey, rowKey);

        if (!await blob.ExistsAsync(cancellationToken))
        {
            return (false, default);
        }

        T? result = await Download(blob, cancellationToken);
        return (result is not null, result);
    }

    public async Task AddEntityAsync(T entity, CancellationToken cancellationToken = default)
    {
        TClient blob = await GetClient(entity);

        if (await blob.ExistsAsync(cancellationToken))
        {
            throw new InvalidOperationException("Entity already exists.");
        }

        await Upload(blob, entity, cancellationToken);
    }

    public async Task UpdateEntityAsync(T entity, CancellationToken cancellationToken = default)
    {
        TClient blob = await GetClient(entity);

        if (!await blob.ExistsAsync(cancellationToken))
        {
            throw new InvalidOperationException("Entity doesn't exist.");
        }

        await Upload(blob, entity, cancellationToken);
    }

    public async Task UpsertEntityAsync(T entity, CancellationToken cancellationToken = default)
    {
        TClient blob = await GetClient(entity);
        await Upload(blob, entity, cancellationToken);
    }

    public Task DeleteEntityAsync(T entity, CancellationToken cancellationToken = default) => DeleteEntityAsync(entity.PartitionKey, entity.RowKey, cancellationToken);

    public async Task DeleteEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        TClient blob = await GetClient(partitionKey, rowKey);
        await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task DeleteAllEntitiesAsync(string partitionKey, CancellationToken cancellationToken = default)
    {
        if (partitionKey is null)
        {
            throw new ArgumentNullException(nameof(partitionKey));
        }

        BlobContainerClient container = await _containerClient;

        string prefix = partitionKey + '/';

        await foreach (BlobHierarchyItem blob in container.GetBlobsByHierarchyAsync(BlobTraits.None, BlobStates.None, prefix: prefix, delimiter: "/", cancellationToken: cancellationToken))
        {
            TClient blobClient = GetClient(container, blob.Blob.Name);
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
    }

    public Task<bool> ExistsAsync(T entity, CancellationToken cancellationToken = default) => ExistsAsync(entity.PartitionKey, entity.RowKey, cancellationToken);

    public async Task<bool> ExistsAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        TClient blobClient = await GetClient(partitionKey, rowKey);
        return await blobClient.ExistsAsync(cancellationToken);
    }

    protected abstract Task Upload(TClient blob, T entity, CancellationToken cancellationToken);

    protected Dictionary<string, string> CreateTags(T entity)
    {
        Dictionary<string, string> tags = new(2 + _tags.Count)
        {
            [PartitionTagConstant] = entity.PartitionKey,
            [RowTagConstant] = entity.RowKey
        };

        foreach (string tag in _tags)
        {
            object? tagValue = entity[tag];

            if (tagValue is not null)
            {
                tags[tag] = tagValue.ToString();
            }
        }

        return tags;
    }

    private async Task<T?> Download(TClient blob, CancellationToken cancellationToken)
    {
        using Stream stream = await blob.OpenReadAsync(cancellationToken: cancellationToken);
        return await _options.Serializer.DeserializeAsync<T>(stream, cancellationToken);
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => QueryAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);

    public IAsyncEnumerable<T> QueryAsync(CancellationToken cancellationToken = default) => QueryAsync(null!, cancellationToken);

    public async IAsyncEnumerable<T> QueryAsync(Expression<Func<T, bool>> filter, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach ((TClient _, LazyAsync<T?> lazyEntity) in QueryInternalAsync(filter, cancellationToken))
        {
            T? entity = await lazyEntity;
            if (entity is not null)
            {
                yield return entity;
            }
        }
    }

    internal IAsyncEnumerable<(TClient client, LazyAsync<T?> entity)> QueryInternalAsync(Expression<Func<T, bool>>? filter, CancellationToken cancellationToken = default)
    {
        if (filter is null)
        {
            return IterateAllBlobs(cancellationToken);
        }

        BlobQueryVisitor visitor = new(_partitionKeyProxy, _rowKeyProxy, _tags);
        Expression<Func<T, bool>> visitedFilter = visitor.VisitAndConvert(filter, nameof(QueryInternalAsync));

        if (!visitor.Error)
        {
            if (_options.UseTags)
            {
                if (visitor.SimpleFilter)
                {
                    return IterateBlobsByTag(visitor.Filter!, cancellationToken);
                }

                return IterateBlobsByTagAndComplexFilter(visitor.Filter!, filter, cancellationToken);
            }
        }

        if (visitor.OperandError && _options.UseTags)
        {
            return IterateFilteredByTagsAtRuntime(filter, visitor.TagOnlyFilter, cancellationToken);
        }

        return IterateFilteredAtRuntime(filter, cancellationToken);
    }

    private async IAsyncEnumerable<(TClient client, LazyAsync<T?> entity)> IterateAllBlobs([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        BlobContainerClient container = await _containerClient;

        await foreach (BlobItem blob in container.GetBlobsAsync(BlobTraits.Tags, BlobStates.None, cancellationToken: cancellationToken))
        {
            TClient client = GetClient(container, blob.Name);
            yield return (client, new(() => Download(client, cancellationToken)));
        }
    }

    private async IAsyncEnumerable<(TClient client, LazyAsync<T?> entity)> IterateBlobsByTag(string filter, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!_options.UseTags)
        {
            throw new InvalidOperationException("Tags is disabled yet we ended up in a tags call");
        }

        BlobContainerClient container = await _containerClient;

        await foreach (TaggedBlobItem blob in container.FindBlobsByTagsAsync(filter, cancellationToken))
        {
            TClient client = GetClient(container, blob.BlobName);
            yield return (client, new(() => Download(client, cancellationToken)));
        }
    }

    private async IAsyncEnumerable<(TClient client, LazyAsync<T?> entity)> IterateBlobsByTagAndComplexFilter(string filter, LazyFilteringExpression<T> compiledFilter, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach ((TClient client, LazyAsync<T?> entity) result in IterateBlobsByTag(filter, cancellationToken))
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
        await foreach ((TClient client, LazyAsync<T?> entity) result in IterateAllBlobs(cancellationToken))
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
        if (!_options.UseTags)
        {
            throw new InvalidOperationException("Tags is disabled yet we ended up in a tags call");
        }

        LazyFilteringExpression<T> originalCompiledFilter = filter;

        BlobTagQueryVisitor<T> visitor = new(_partitionKeyProxy, _rowKeyProxy, _tags);
        var visitedFilter = (Expression<Func<BlobTagAccessor, bool>>)visitor.Visit(filter);
        LazyFilteringExpression<BlobTagAccessor> compiledFilter = visitedFilter;

        BlobContainerClient container = await _containerClient;
        await foreach (BlobItem blob in container.GetBlobsAsync(BlobTraits.Tags, BlobStates.None, cancellationToken: cancellationToken))
        {
            BlobTagAccessor tags = new(blob.Tags);

            if (compiledFilter.Invoke(tags))
            {
                TClient client = GetClient(container, blob.Name);
                LazyAsync<T?> entity = new(() => Download(client, cancellationToken));

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