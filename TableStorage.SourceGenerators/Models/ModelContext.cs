using TableStorage.SourceGenerators.Utilities;

namespace TableStorage.SourceGenerators.Models;

/// <summary>
/// Context information used during code generation for a specific model class.
/// Contains metadata about key mappings, change tracking, and proxy configurations.
/// </summary>
internal readonly struct ModelContext(
    bool hasChangeTracking, 
    bool hasPartitionKeyProxy, 
    bool hasRowKeyProxy, 
    PrettyMemberToGenerate partitionKeyProxy, 
    PrettyMemberToGenerate rowKeyProxy, 
    string realPartitionKey, 
    string realRowKey) : IEquatable<ModelContext>
{
    /// <summary>
    /// Indicates whether change tracking is enabled for this model.
    /// </summary>
    public readonly bool HasChangeTracking = hasChangeTracking;
    
    /// <summary>
    /// Indicates whether a partition key proxy is configured.
    /// </summary>
    public readonly bool HasPartitionKeyProxy = hasPartitionKeyProxy;
    
    /// <summary>
    /// Indicates whether a row key proxy is configured.
    /// </summary>
    public readonly bool HasRowKeyProxy = hasRowKeyProxy;
    
    /// <summary>
    /// The partition key proxy member configuration.
    /// </summary>
    public readonly PrettyMemberToGenerate PartitionKeyProxy = partitionKeyProxy; 
    
    /// <summary>
    /// The row key proxy member configuration.
    /// </summary>
    public readonly PrettyMemberToGenerate RowKeyProxy = rowKeyProxy;
    
    /// <summary>
    /// The actual partition key property name to use.
    /// </summary>
    public readonly string RealPartitionKey = realPartitionKey;
    
    /// <summary>
    /// The actual row key property name to use.
    /// </summary>
    public readonly string RealRowKey = realRowKey;

    public bool Equals(ModelContext other)
    {
        return HasChangeTracking == other.HasChangeTracking && 
               HasPartitionKeyProxy == other.HasPartitionKeyProxy && 
               HasRowKeyProxy == other.HasRowKeyProxy && 
               PartitionKeyProxy.Equals(other.PartitionKeyProxy) && 
               RowKeyProxy.Equals(other.RowKeyProxy) && 
               RealPartitionKey == other.RealPartitionKey && 
               RealRowKey == other.RealRowKey;
    }

    public override bool Equals(object? obj)
    {
        return obj is ModelContext other && Equals(other);
    }    public override int GetHashCode()
    {
        var hashCode = HashCode.Create();
        hashCode.Add(HasChangeTracking);
        hashCode.Add(HasPartitionKeyProxy);
        hashCode.Add(HasRowKeyProxy);
        hashCode.Add(PartitionKeyProxy);
        hashCode.Add(RowKeyProxy);
        hashCode.Add(RealPartitionKey);
        hashCode.Add(RealRowKey);
        return hashCode.ToHashCode();
    }
}
