using Microsoft.CodeAnalysis;
using TableStorage.SourceGenerators.Utilities;

namespace TableStorage.SourceGenerators.Models;

/// <summary>
/// Represents context information for generating a class in a table context.
/// </summary>
public readonly struct ContextClassToGenerate(string name, string @namespace, EquatableArray<ContextMemberToGenerate> members) : IEquatable<ContextClassToGenerate>
{
    public readonly string Name = name;
    public readonly string Namespace = @namespace;
    public readonly EquatableArray<ContextMemberToGenerate> Members = members;

    public bool Equals(ContextClassToGenerate other)
    {
        return Name == other.Name && Namespace == other.Namespace && Members.Equals(other.Members);
    }

    public override bool Equals(object? obj)
    {
        return obj is ContextClassToGenerate other && Equals(other);
    }    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Namespace, Members);
    }
}

/// <summary>
/// Represents a member to be generated in a context class.
/// </summary>
public readonly struct ContextMemberToGenerate(string name, string type, TypeKind typeKind, string setType) : IEquatable<ContextMemberToGenerate>
{
    public readonly string Name = name;
    public readonly string Type = type;
    public readonly TypeKind TypeKind = typeKind;
    public readonly string SetType = setType;

    public bool Equals(ContextMemberToGenerate other)
    {
        return Name == other.Name && Type == other.Type && TypeKind == other.TypeKind && SetType == other.SetType;
    }

    public override bool Equals(object? obj)
    {
        return obj is ContextMemberToGenerate other && Equals(other);
    }    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Type, TypeKind, SetType);
    }
}

/// <summary>
/// Represents a complete class configuration for code generation.
/// Contains all information needed to generate a table storage entity class.
/// </summary>
public readonly struct ClassToGenerate(string name, string @namespace, EquatableArray<MemberToGenerate> members, EquatableArray<PrettyMemberToGenerate> prettyMembers, bool withBlobSupport, bool withTablesSupport) : IEquatable<ClassToGenerate>
{
    public readonly string Name = name;
    public readonly string Namespace = @namespace;
    public readonly EquatableArray<MemberToGenerate> Members = members;
    public readonly EquatableArray<PrettyMemberToGenerate> PrettyMembers = prettyMembers;
    public readonly bool WithBlobSupport = withBlobSupport;
    public readonly bool WithTablesSupport = withTablesSupport;/// <summary>
    /// Attempts to find a pretty member with the specified proxy name.
    /// </summary>
    /// <param name="proxy">The proxy name to search for.</param>
    /// <param name="prettyMemberToGenerate">The found pretty member, if any.</param>
    /// <returns>True if a matching pretty member was found, false otherwise.</returns>
    public bool TryGetPrettyMember(string proxy, out PrettyMemberToGenerate prettyMemberToGenerate)
    {
        foreach (PrettyMemberToGenerate member in PrettyMembers)
        {
            if (member.Proxy == proxy)
            {
                prettyMemberToGenerate = member;
                return true;
            }
        }

        prettyMemberToGenerate = default;
        return false;
    }

    public bool Equals(ClassToGenerate other)
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
        return obj is ClassToGenerate other && Equals(other);
    }    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Namespace, Members, PrettyMembers, WithBlobSupport, WithTablesSupport);
    }
}

/// <summary>
/// Represents a member (property) to be generated in a class.
/// Contains all configuration needed for property generation including type information and behavior.
/// </summary>
public readonly struct MemberToGenerate(string name, string type, TypeKind typeKind, bool generateProperty, string partitionKeyProxy, string rowKeyProxy, bool withChangeTracking, bool isPartial, bool tagBlob) : IEquatable<MemberToGenerate>
{
    public readonly string Name = name;
    public readonly string Type = type;
    public readonly TypeKind TypeKind = typeKind;
    public readonly bool GenerateProperty = generateProperty;
    public readonly string PartitionKeyProxy = partitionKeyProxy;
    public readonly string RowKeyProxy = rowKeyProxy;
    public readonly bool WithChangeTracking = generateProperty && withChangeTracking;
    public readonly bool IsPartial = isPartial;
    public readonly bool TagBlob = tagBlob;

    public bool Equals(MemberToGenerate other)
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
        return obj is MemberToGenerate other && Equals(other);
    }    public override int GetHashCode()
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
/// Represents a "pretty" member that serves as a proxy for another property.
/// Used for creating friendly property names that map to underlying table entity properties.
/// </summary>
public readonly struct PrettyMemberToGenerate(string name, string proxy) : IEquatable<PrettyMemberToGenerate>
{
    public readonly string Name = name;
    public readonly string Proxy = proxy;

    public bool Equals(PrettyMemberToGenerate other)
    {
        return Name == other.Name && Proxy == other.Proxy;
    }

    public override bool Equals(object? obj)
    {
        return obj is PrettyMemberToGenerate other && Equals(other);
    }    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Proxy);
    }
}
