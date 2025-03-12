using Azure.Storage.Blobs.Models;

namespace TableStorage;

public sealed class BlobSet<T> : BaseBlobSet<T, BlobClient>
    where T : IBlobEntity
{
    internal BlobSet(BlobStorageFactory factory, string tableName, BlobOptions options, string? partitionKeyProxy, string? rowKeyProxy, IReadOnlyCollection<string> tags)
        : base(factory, tableName, options, partitionKeyProxy, rowKeyProxy, tags)
    {
    }

    protected override BlobClient GetClient(BlobContainerClient containerClient, string id) => containerClient.GetBlobClient(id);

    protected override Task Upload(BlobClient blob, T entity, CancellationToken cancellationToken)
    {
        BinaryData data = _options.Serializer.Serialize(entity);

        BlobUploadOptions? options = null;

        if (_options.UseTags && !_options.IsHierarchical)
        {
            Dictionary<string, string> tags = CreateTags(entity);

            options = new()
            {
                Tags = tags
            };
        }

        return blob.UploadAsync(data, options, cancellationToken);
    }
}
