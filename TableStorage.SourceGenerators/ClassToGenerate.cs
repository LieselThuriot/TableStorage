﻿using Microsoft.CodeAnalysis;

namespace TableStorage.SourceGenerators;

public readonly struct ClassToGenerate(string name, string @namespace, List<MemberToGenerate> members, List<PrettyMemberToGenerate> prettyMembers)
{
    public readonly string Name = name;
    public readonly string Namespace = @namespace;
    public readonly List<MemberToGenerate> Members = members;
    public readonly List<PrettyMemberToGenerate> PrettyMembers = prettyMembers;

    public bool TryGetPrettyMember(string proxy, out PrettyMemberToGenerate prettyMemberToGenerate)
    {
        foreach (var member in PrettyMembers)
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

public readonly struct MemberToGenerate(string name, string type, TypeKind typeKind, bool generateProperty, string partitionKeyProxy, string rowKeyProxy, bool withChangeTracking, bool isPartial)
{
    public readonly string Name = name;
    public readonly string Type = type;
    public readonly TypeKind TypeKind = typeKind;
    public readonly bool GenerateProperty = generateProperty || isPartial;
    public readonly string PartitionKeyProxy = partitionKeyProxy;
    public readonly string RowKeyProxy = rowKeyProxy;
    public readonly bool WithChangeTracking = (generateProperty || isPartial) && withChangeTracking;
    public readonly bool IsPartial = isPartial;
}

public readonly struct PrettyMemberToGenerate(string name, string proxy)
{
    public readonly string Name = name;
    public readonly string Proxy = proxy;
}