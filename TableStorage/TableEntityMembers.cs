using System.Reflection;

namespace TableStorage;

internal static class TableEntityMembers
{
    public static readonly PropertyInfo PartitionKey = typeof(ITableEntity).GetProperty(nameof(ITableEntity.PartitionKey))!;
    public static readonly PropertyInfo RowKey = typeof(ITableEntity).GetProperty(nameof(ITableEntity.RowKey))!;
}
