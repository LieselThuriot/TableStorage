using Azure.Storage.Blobs.Specialized;
using System.Linq.Expressions;

namespace TableStorage.Linq;

internal static class BlobSetQueryHelperExtensions
{
    internal static async Task<int> BatchUpdateAsync<T, TClient>(this BlobSetQueryHelper<T, TClient> setHelper, Expression<Func<T, T>> update, CancellationToken token = default)
        where TClient : BlobBaseClient
        where T : IBlobEntity
    {
        int count = 0;

        LazyExpression<T> compiledUpdate = update;
        await foreach (T? entity in setHelper.WithCancellation(token))
        {
            T updatedEntity = compiledUpdate.Invoke(entity);
            await setHelper.Table.UpsertEntityAsync(updatedEntity, token);
            count++;
        }

        return count;
    }
}