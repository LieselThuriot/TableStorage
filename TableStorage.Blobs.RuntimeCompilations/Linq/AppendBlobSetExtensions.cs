﻿using System.Linq.Expressions;

namespace TableStorage.Linq;

public static class AppendBlobSetExtensions
{
    public static Task<int> BatchUpdateAsync<T>(this AppendBlobSet<T> blobset, Expression<Func<T, T>> update, CancellationToken token = default)
        where T : IBlobEntity
    {
        return BlobSetQueryHelperExtensions.BatchUpdateAsync(BlobSetQueryHelper.CreateHelper(blobset), update, token);
    }
}
