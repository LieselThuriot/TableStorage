using Microsoft.CodeAnalysis;

namespace TableStorage.SourceGenerators.Models;

/// <summary>
/// Represents context information for generating a class in a table context.
/// </summary>
public readonly struct ContextClassToGenerate(string name, string @namespace, List<ContextMemberToGenerate> members)
{
    public readonly string Name = name;
    public readonly string Namespace = @namespace;
    public readonly List<ContextMemberToGenerate> Members = members;
}

/// <summary>
/// Represents a member to be generated in a context class.
/// </summary>
public readonly struct ContextMemberToGenerate(string name, string type, TypeKind typeKind, string setType)
{
    public readonly string Name = name;
    public readonly string Type = type;
    public readonly TypeKind TypeKind = typeKind;
    public readonly string SetType = setType;
}

/// <summary>
/// Represents a complete class configuration for code generation.
/// Contains all information needed to generate a table storage entity class.
/// </summary>
public readonly struct ClassToGenerate(string name, string @namespace, List<MemberToGenerate> members, List<PrettyMemberToGenerate> prettyMembers, bool withBlobSupport, bool withTablesSupport)
{
    public readonly string Name = name;
    public readonly string Namespace = @namespace;
    public readonly List<MemberToGenerate> Members = members;
    public readonly List<PrettyMemberToGenerate> PrettyMembers = prettyMembers;
    public readonly bool WithBlobSupport = withBlobSupport;
    public readonly bool WithTablesSupport = withTablesSupport;

    /// <summary>
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
}

/// <summary>
/// Represents a member (property) to be generated in a class.
/// Contains all configuration needed for property generation including type information and behavior.
/// </summary>
public readonly struct MemberToGenerate(string name, string type, TypeKind typeKind, bool generateProperty, string partitionKeyProxy, string rowKeyProxy, bool withChangeTracking, bool isPartial, bool tagBlob)
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
}

/// <summary>
/// Represents a "pretty" member that serves as a proxy for another property.
/// Used for creating friendly property names that map to underlying table entity properties.
/// </summary>
public readonly struct PrettyMemberToGenerate(string name, string proxy)
{
    public readonly string Name = name;
    public readonly string Proxy = proxy;
}
