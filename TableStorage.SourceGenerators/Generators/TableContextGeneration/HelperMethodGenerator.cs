using System.Text;

namespace TableStorage.SourceGenerators.Generators.TableContextGeneration;

/// <summary>
/// Generates helper methods for creating table and blob sets.
/// </summary>
internal static class HelperMethodGenerator
{
    /// <summary>
    /// Generates all helper methods based on available capabilities.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="hasTables">Whether table support is available.</param>
    /// <param name="hasBlobs">Whether blob support is available.</param>
    public static void GenerateHelperMethods(StringBuilder sb, bool hasTables, bool hasBlobs)
    {
        if (hasBlobs)
        {
            GenerateBlobHelperMethods(sb);
        }

        if (hasTables)
        {
            GenerateTableHelperMethods(sb);
        }
    }

    private static void GenerateBlobHelperMethods(StringBuilder sb)
    {
        sb.Append(@"

        public BlobSet<T> GetBlobSet<T>(string tableName)
            where T : class, TableStorage.IBlobEntity, new()
        {
            return _blobCreator.CreateSet<T>(tableName);
        }

        public BlobSet<T> GetBlobSet<T>(string tableName, string partitionKeyProxy = null, string rowKeyProxy = null)
            where T : class, TableStorage.IBlobEntity, new()
        {
            return _blobCreator.CreateSet<T>(tableName, partitionKeyProxy, rowKeyProxy);
        }

        public AppendBlobSet<T> GetAppendBlobSet<T>(string tableName)
            where T : class, TableStorage.IBlobEntity, new()
        {
            return _blobCreator.CreateAppendSet<T>(tableName);
        }

        public AppendBlobSet<T> GetAppendBlobSet<T>(string tableName, string partitionKeyProxy = null, string rowKeyProxy = null)
            where T : class, TableStorage.IBlobEntity, new()
        {
            return _blobCreator.CreateAppendSet<T>(tableName, partitionKeyProxy, rowKeyProxy);
        }");
    }

    private static void GenerateTableHelperMethods(StringBuilder sb)
    {
        sb.Append(@"

        public TableSet<T> GetTableSet<T>(string tableName)
            where T : class, Azure.Data.Tables.ITableEntity, new()
        {
            return _creator.CreateSet<T>(tableName);
        }

        public TableSet<T> GetTableSet<T>(string tableName, string partitionKeyProxy = null, string rowKeyProxy = null)
            where T : class, Azure.Data.Tables.ITableEntity, new()
        {
            return _creator.CreateSet<T>(tableName, partitionKeyProxy, rowKeyProxy);
        }

        public TableSet<T> GetTableSetWithChangeTracking<T>(string tableName)
            where T : class, Azure.Data.Tables.ITableEntity, TableStorage.IChangeTracking, new()
        {
            return _creator.CreateSetWithChangeTracking<T>(tableName);
        }

        public TableSet<T> GetTableSetWithChangeTracking<T>(string tableName, string partitionKeyProxy = null, string rowKeyProxy = null)
            where T : class, Azure.Data.Tables.ITableEntity, TableStorage.IChangeTracking, new()
        {
            return _creator.CreateSetWithChangeTracking<T>(tableName, partitionKeyProxy, rowKeyProxy);
        }");
    }
}