using TableStorage.SourceGenerators.Utilities;

namespace TableStorage.SourceGenerators.Models;

/// <summary>
/// Represents extracted information from a class with TableContextAttribute.
/// This is a value-type data model that enables proper caching in incremental generators.
/// </summary>
internal readonly struct TableContextClassInfo(string name, string @namespace, EquatableArray<TableContextMemberInfo> members) : IEquatable<TableContextClassInfo>
{
    public readonly string Name = name;
    public readonly string Namespace = @namespace;
    public readonly EquatableArray<TableContextMemberInfo> Members = members;

    public bool Equals(TableContextClassInfo other)
    {
        return Name == other.Name && Namespace == other.Namespace && Members.Equals(other.Members);
    }

    public override bool Equals(object? obj)
    {
        return obj is TableContextClassInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Namespace, Members);
    }
}

/// <summary>
/// Represents extracted information from a member in a TableContext class.
/// This is a value-type data model that enables proper caching in incremental generators.
/// </summary>
internal readonly struct TableContextMemberInfo(string name, string type, string typeKind, string setType) : IEquatable<TableContextMemberInfo>
{
    public readonly string Name = name;
    public readonly string Type = type;
    public readonly string TypeKind = typeKind;
    public readonly string SetType = setType;

    public bool Equals(TableContextMemberInfo other)
    {
        return Name == other.Name && Type == other.Type && TypeKind == other.TypeKind && SetType == other.SetType;
    }

    public override bool Equals(object? obj)
    {
        return obj is TableContextMemberInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Type, TypeKind, SetType);
    }
}

/// <summary>
/// Represents extracted information from a class with TableSetAttribute.
/// This is a value-type data model that enables proper caching in incremental generators.
/// </summary>
internal readonly struct TableSetClassInfo(string name, string @namespace, EquatableArray<TableSetMemberInfo> members, 
    EquatableArray<TableSetPrettyMemberInfo> prettyMembers, bool withBlobSupport, bool withTablesSupport) : IEquatable<TableSetClassInfo>
{
    public readonly string Name = name;
    public readonly string Namespace = @namespace;
    public readonly EquatableArray<TableSetMemberInfo> Members = members;
    public readonly EquatableArray<TableSetPrettyMemberInfo> PrettyMembers = prettyMembers;
    public readonly bool WithBlobSupport = withBlobSupport;
    public readonly bool WithTablesSupport = withTablesSupport;

    public bool Equals(TableSetClassInfo other)
    {
        return Name == other.Name && 
               Namespace == other.Namespace && 
               Members.Equals(other.Members) && 
               PrettyMembers.Equals(other.PrettyMembers) && 
               WithBlobSupport == other.WithBlobSupport && 
               WithTablesSupport == other.WithTablesSupport;
    }

    public override bool Equals(object? obj)
    {
        return obj is TableSetClassInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Namespace, Members, PrettyMembers, WithBlobSupport, WithTablesSupport);
    }
}

/// <summary>
/// Represents extracted information from a member in a TableSet class.
/// This is a value-type data model that enables proper caching in incremental generators.
/// </summary>
internal readonly struct TableSetMemberInfo(string name, string type, string typeKind, bool generateProperty, 
    string partitionKeyProxy, string rowKeyProxy, bool withChangeTracking, bool isPartial, bool tagBlob) : IEquatable<TableSetMemberInfo>
{
    public readonly string Name = name;
    public readonly string Type = type;
    public readonly string TypeKind = typeKind;
    public readonly bool GenerateProperty = generateProperty;
    public readonly string PartitionKeyProxy = partitionKeyProxy;
    public readonly string RowKeyProxy = rowKeyProxy;
    public readonly bool WithChangeTracking = generateProperty && withChangeTracking;
    public readonly bool IsPartial = isPartial;
    public readonly bool TagBlob = tagBlob;

    public bool Equals(TableSetMemberInfo other)
    {
        return Name == other.Name && 
               Type == other.Type && 
               TypeKind == other.TypeKind && 
               GenerateProperty == other.GenerateProperty && 
               PartitionKeyProxy == other.PartitionKeyProxy && 
               RowKeyProxy == other.RowKeyProxy && 
               WithChangeTracking == other.WithChangeTracking && 
               IsPartial == other.IsPartial && 
               TagBlob == other.TagBlob;
    }

    public override bool Equals(object? obj)
    {
        return obj is TableSetMemberInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hashCode = HashCode.Create();
        hashCode.Add(Name);
        hashCode.Add(Type);
        hashCode.Add(TypeKind);
        hashCode.Add(GenerateProperty);
        hashCode.Add(PartitionKeyProxy);
        hashCode.Add(RowKeyProxy);
        hashCode.Add(WithChangeTracking);
        hashCode.Add(IsPartial);
        hashCode.Add(TagBlob);
        return hashCode.ToHashCode();
    }
}

/// <summary>
/// Represents extracted information from a "pretty" member in a TableSet class.
/// This is a value-type data model that enables proper caching in incremental generators.
/// </summary>
internal readonly struct TableSetPrettyMemberInfo(string name, string proxy) : IEquatable<TableSetPrettyMemberInfo>
{
    public readonly string Name = name;
    public readonly string Proxy = proxy;

    public bool Equals(TableSetPrettyMemberInfo other)
    {
        return Name == other.Name && Proxy == other.Proxy;
    }

    public override bool Equals(object? obj)
    {
        return obj is TableSetPrettyMemberInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Proxy);
    }
}

/// <summary>
/// Represents extracted compilation capabilities.
/// This is a value-type data model that enables proper caching in incremental generators.
/// </summary>
internal readonly struct CompilationCapabilities(bool hasTables, bool hasBlobs) : IEquatable<CompilationCapabilities>
{
    public readonly bool HasTables = hasTables;
    public readonly bool HasBlobs = hasBlobs;

    public bool Equals(CompilationCapabilities other)
    {
        return HasTables == other.HasTables && HasBlobs == other.HasBlobs;
    }

    public override bool Equals(object? obj)
    {
        return obj is CompilationCapabilities other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(HasTables, HasBlobs);
    }
}

/// <summary>
/// Represents extracted configuration options.
/// This is a value-type data model that enables proper caching in incremental generators.
/// </summary>
internal readonly struct GenerationOptions(bool publishAot, string? tableStorageSerializerContext) : IEquatable<GenerationOptions>
{
    public readonly bool PublishAot = publishAot;
    public readonly string? TableStorageSerializerContext = tableStorageSerializerContext;

    public bool Equals(GenerationOptions other)
    {
        return PublishAot == other.PublishAot && TableStorageSerializerContext == other.TableStorageSerializerContext;
    }

    public override bool Equals(object? obj)
    {
        return obj is GenerationOptions other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(PublishAot, TableStorageSerializerContext);
    }
}
