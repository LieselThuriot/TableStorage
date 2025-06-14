using System.Text;
using TableStorage.SourceGenerators.Models;

namespace TableStorage.SourceGenerators.Generators.CodeGeneration;

/// <summary>
/// Generates table entity members and related functionality.
/// </summary>
internal static class TableEntityGenerator
{
    /// <summary>
    /// Generates the basic table entity members (PartitionKey, RowKey, Timestamp, ETag).
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="context">The model context.</param>
    public static void GenerateTableEntityMembers(StringBuilder sb, in ModelContext context)
    {
        sb.Append(@"
        ");

        if (context.HasPartitionKeyProxy)
        {
            sb.Append("string Azure.Data.Tables.ITableEntity.PartitionKey { get => ").Append(context.PartitionKeyProxy.Name).Append("; set => ").Append(context.PartitionKeyProxy.Name).Append(" = value; }");
        }
        else
        {
            sb.Append("public string PartitionKey { get; set; }");
        }

        sb.Append(@"
        ");

        if (context.HasRowKeyProxy)
        {
            sb.Append("string Azure.Data.Tables.ITableEntity.RowKey { get => ").Append(context.RowKeyProxy.Name).Append("; set => ").Append(context.RowKeyProxy.Name).Append(" = value; }");
        }
        else
        {
            sb.Append("public string RowKey { get; set; }");
        }

        sb.Append(@"
        public DateTimeOffset? Timestamp { get; set; }
        public Azure.ETag ETag { get; set; }");
    }
}
