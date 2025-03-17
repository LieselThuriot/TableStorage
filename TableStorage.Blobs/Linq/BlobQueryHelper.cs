using Azure.Storage.Blobs.Specialized;
using System.Linq.Expressions;

namespace TableStorage.Linq;

public static class BlobSetQueryHelper
{
    private static BlobSetQueryHelper<T, TClient> CreateHelper<T, TClient>(BaseBlobSet<T, TClient> client)
        where TClient : BlobBaseClient
        where T : IBlobEntity
    {
        return new BlobSetQueryHelper<T, TClient>(client);
    }

    public static IFilteredBlobQueryable<T> Where<T>(this BlobSet<T> table, Expression<Func<T, bool>> predicate)
        where T : IBlobEntity
    {
        return CreateHelper(table).Where(predicate);
    }

    public static IFilteredBlobQueryable<T> ExistsIn<T, TElement>(this BlobSet<T> table, Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
        where T : IBlobEntity
    {
        return CreateHelper(table).ExistsIn(predicate, elements);
    }

    public static IFilteredBlobQueryable<T> NotExistsIn<T, TElement>(this BlobSet<T> table, Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
        where T : IBlobEntity
    {
        return CreateHelper(table).NotExistsIn(predicate, elements);
    }

    public static Task<T> FirstAsync<T>(this BlobSet<T> table, CancellationToken token = default)
        where T : IBlobEntity
    {
        return CreateHelper(table).FirstAsync(token);
    }

    public static Task<T?> FirstOrDefaultAsync<T>(this BlobSet<T> table, CancellationToken token = default)
        where T : IBlobEntity
    {
        return CreateHelper(table).FirstOrDefaultAsync(token);
    }

    public static Task<T> SingleAsync<T>(this BlobSet<T> table, CancellationToken token = default)
        where T : IBlobEntity
    {
        return CreateHelper(table).SingleAsync(token);
    }

    public static Task<T?> SingleOrDefaultAsync<T>(this BlobSet<T> table, CancellationToken token = default)
        where T : IBlobEntity
    {
        return CreateHelper(table).SingleOrDefaultAsync(token);
    }

    public static IBlobAsyncEnumerable<T> WhereId<T>(this BlobSet<T> table, Func<BlobId, bool> predicate)
        where T : IBlobEntity
    {
        return CreateHelper(table).WhereId(predicate);
    }

    public static IFilteredBlobQueryable<T> Where<T>(this AppendBlobSet<T> table, Expression<Func<T, bool>> predicate)
        where T : IBlobEntity
    {
        return CreateHelper(table).Where(predicate);
    }

    public static IFilteredBlobQueryable<T> ExistsIn<T, TElement>(this AppendBlobSet<T> table, Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
        where T : IBlobEntity
    {
        return CreateHelper(table).ExistsIn(predicate, elements);
    }

    public static IFilteredBlobQueryable<T> NotExistsIn<T, TElement>(this AppendBlobSet<T> table, Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
        where T : IBlobEntity
    {
        return CreateHelper(table).NotExistsIn(predicate, elements);
    }

    public static Task<T> FirstAsync<T>(this AppendBlobSet<T> table, CancellationToken token = default)
        where T : IBlobEntity
    {
        return CreateHelper(table).FirstAsync(token);
    }

    public static Task<T?> FirstOrDefaultAsync<T>(this AppendBlobSet<T> table, CancellationToken token = default)
        where T : IBlobEntity
    {
        return CreateHelper(table).FirstOrDefaultAsync(token);
    }

    public static Task<T> SingleAsync<T>(this AppendBlobSet<T> table, CancellationToken token = default)
        where T : IBlobEntity
    {
        return CreateHelper(table).SingleAsync(token);
    }

    public static Task<T?> SingleOrDefaultAsync<T>(this AppendBlobSet<T> table, CancellationToken token = default)
        where T : IBlobEntity
    {
        return CreateHelper(table).SingleOrDefaultAsync(token);
    }

    public static IBlobAsyncEnumerable<T> WhereId<T>(this AppendBlobSet<T> table, Func<BlobId, bool> predicate)
        where T : IBlobEntity
    {
        return CreateHelper(table).WhereId(predicate);
    }
}
