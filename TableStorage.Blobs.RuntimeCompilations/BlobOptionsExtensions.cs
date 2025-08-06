using Azure.Storage.Blobs.Specialized;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace TableStorage;
public static class BlobOptionsExtensions
{
    public static void EnableCompilationAtRuntime(this BlobOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        options.QueryHandlerFactory = new CompilationQueryHandlerFactory();
    }

    internal sealed class CompilationQueryHandlerFactory : IQueryHandlerFactory
    {
        public async IAsyncEnumerable<(TClient client, LazyAsync<T?> entity)> QueryAsync<T, TClient>(BaseBlobSet<T, TClient> baseBlobSet, Expression<Func<T, bool>> filter, [EnumeratorCancellation] CancellationToken cancellationToken)
            where T : IBlobEntity
            where TClient : BlobBaseClient
        {
            CompiledBlobQueryHandler<T, TClient> handler = new(baseBlobSet);

            await foreach ((TClient client, LazyAsync<T?> entity) result in handler.QueryAsync(filter, cancellationToken))
            {
                yield return result;
            }
        }
    }
}