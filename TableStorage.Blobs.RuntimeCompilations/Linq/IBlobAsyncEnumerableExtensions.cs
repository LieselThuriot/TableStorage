using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using System.Linq.Expressions;

namespace TableStorage.Linq;

public static class IBlobAsyncEnumerableExtensions
{
    public static Task<int> BatchUpdateAsync<T>(this IBlobAsyncEnumerable<T> blobset, Expression<Func<T, T>> update, CancellationToken token = default)
        where T : IBlobEntity
    {
        return blobset switch
        {
            BlobSetQueryHelper<T, AppendBlobClient> append => BlobSetQueryHelperExtensions.BatchUpdateAsync(append, update, token),
            BlobSetQueryHelper<T, BlobClient> block => BlobSetQueryHelperExtensions.BatchUpdateAsync(block, update, token),
            _ => throw new NotSupportedException("blobset type does not support batch updates.")
        };
    }
}