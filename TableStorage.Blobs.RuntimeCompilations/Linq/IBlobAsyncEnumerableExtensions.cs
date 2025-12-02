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
            BlobSetQueryHelper<T, AppendBlobClient> append => append.BatchUpdateAsync(update, token),
            BlobSetQueryHelper<T, BlobClient> block => block.BatchUpdateAsync(update, token),
            _ => throw new NotSupportedException("blobset type does not support batch updates.")
        };
    }
}