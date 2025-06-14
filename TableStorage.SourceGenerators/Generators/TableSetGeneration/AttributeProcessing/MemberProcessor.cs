using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using TableStorage.SourceGenerators.Models;
using TableStorage.SourceGenerators.Utilities;

namespace TableStorage.SourceGenerators.Generators.TableSetGeneration.AttributeProcessing;

/// <summary>
/// Processes class members to determine which properties should be generated and how.
/// </summary>
internal static class MemberProcessor
{    /// <summary>
    /// Reserved property names that should not be processed as regular members.
    /// </summary>
    private static readonly HashSet<string> s_reservedPropertyNames =
    [
        "PartitionKey",
        "RowKey", 
        "Timestamp",
        "ETag",
        "this[]", // Indexer
        "Keys",   // From IDictionary
        "Values", // From IDictionary
        "Count",  // From IDictionary
        "IsReadOnly" // From IDictionary
    ];

    /// <summary>
    /// Processes all members of a class symbol to generate appropriate member configurations.
    /// </summary>
    /// <param name="classSymbol">The class symbol to process.</param>
    /// <param name="prettyMembers">List of pretty members (proxies) already configured.</param>
    /// <param name="withChangeTracking">Whether change tracking should be enabled.</param>
    /// <param name="partitionKeyForNewMembers">Partition key proxy for new members.</param>
    /// <param name="rowKeyForNewMembers">Row key proxy for new members.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of members to generate.</returns>
    public static List<MemberToGenerate> ProcessClassMembers(
        INamedTypeSymbol classSymbol, 
        List<PrettyMemberToGenerate> prettyMembers, 
        bool withChangeTracking, 
        string partitionKeyForNewMembers, 
        string rowKeyForNewMembers, 
        CancellationToken ct)
    {
        ImmutableArray<ISymbol> classMembersSymbols = classSymbol.GetMembers();
        List<MemberToGenerate> members = new(classMembersSymbols.Length);

        foreach (ISymbol memberSymbol in classMembersSymbols)
        {
            ct.ThrowIfCancellationRequested();
            
            if (memberSymbol is not IPropertySymbol property)
            {
                continue;
            }

            if (s_reservedPropertyNames.Contains(property.Name))
            {
                continue;
            }

            ITypeSymbol type = property.Type;
            TypeKind typeKind = TypeHelper.GetTypeKind(type);
            bool tagBlob = property.GetAttributes().Any(x => x.AttributeClass?.ToDisplayString() == "TableStorage.TagAttribute");
            bool generate = property.IsPartialDefinition && !prettyMembers.Any(x => x.Name == property.Name);

            members.Add(new MemberToGenerate(
                name: property.Name,
                type: type.ToDisplayString(),
                typeKind: typeKind,
                generateProperty: generate,
                partitionKeyProxy: partitionKeyForNewMembers, // Proxies are for TableSetPropertyAttribute, not existing properties
                rowKeyProxy: rowKeyForNewMembers,       // Proxies are for TableSetPropertyAttribute, not existing properties
                withChangeTracking: withChangeTracking,
                isPartial: property.IsPartialDefinition,
                tagBlob: tagBlob
            ));
        }

        return members;
    }
}
