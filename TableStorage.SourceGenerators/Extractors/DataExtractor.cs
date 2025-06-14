using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TableStorage.SourceGenerators.Models;
using TableStorage.SourceGenerators.Utilities;

namespace TableStorage.SourceGenerators.Extractors;

/// <summary>
/// Extracts data from syntax nodes and semantic models to create cacheable data models.
/// This follows incremental generator best practices by extracting all necessary information
/// from syntax nodes in the transform stage, avoiding the need to store syntax nodes in the pipeline.
/// </summary>
internal static class DataExtractor
{
    /// <summary>
    /// Extracts table context class information from a GeneratorAttributeSyntaxContext.
    /// This method is designed to be used in the transform stage of ForAttributeWithMetadataName.
    /// </summary>
    /// <param name="context">The generator attribute syntax context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The extracted table context class information, or null if extraction fails.</returns>
    public static TableContextClassInfo? ExtractTableContextInfo(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetNode is not ClassDeclarationSyntax classDeclaration)
        {
            return null;
        }

        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken) is not INamedTypeSymbol classSymbol)
        {
            return null;
        }

        // Extract basic class information
        string name = classSymbol.Name;
        string @namespace = classSymbol.ContainingNamespace.ToDisplayString();

        // Extract members
        var members = new List<TableContextMemberInfo>();
        
        foreach (var member in classSymbol.GetMembers())
        {
            if (member is IPropertySymbol property && IsTableContextMember(property))
            {
                var memberInfo = ExtractTableContextMemberInfo(property);
                if (memberInfo.HasValue)
                {
                    members.Add(memberInfo.Value);
                }
            }
        }

        return new TableContextClassInfo(name, @namespace, new EquatableArray<TableContextMemberInfo>([.. members]));
    }

    /// <summary>
    /// Extracts table set class information from a GeneratorAttributeSyntaxContext.
    /// This method is designed to be used in the transform stage of ForAttributeWithMetadataName.
    /// </summary>
    /// <param name="context">The generator attribute syntax context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The extracted table set class information, or null if extraction fails.</returns>
    public static TableSetClassInfo? ExtractTableSetInfo(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetNode is not ClassDeclarationSyntax classDeclaration)
        {
            return null;
        }

        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken) is not INamedTypeSymbol classSymbol)
        {
            return null;
        }

        // Extract basic class information
        string name = classSymbol.Name;
        string @namespace = classSymbol.ContainingNamespace.ToDisplayString();

        // Extract attribute information
        var tableSetAttribute = GetTableSetAttribute(classSymbol);
        if (tableSetAttribute == null)
        {
            return null;
        }

        bool withBlobSupport = GetAttributeProperty<bool>(tableSetAttribute, "SupportBlobs");
        bool withTablesSupport = !GetAttributeProperty<bool>(tableSetAttribute, "DisableTables");

        // Extract members and pretty members
        var members = new List<TableSetMemberInfo>();
        var prettyMembers = new List<TableSetPrettyMemberInfo>();

        ExtractTableSetMembers(classSymbol, tableSetAttribute, members, prettyMembers);        return new TableSetClassInfo(
            name, 
            @namespace, 
            new EquatableArray<TableSetMemberInfo>([.. members]),
            new EquatableArray<TableSetPrettyMemberInfo>([.. prettyMembers]),
            withBlobSupport,
            withTablesSupport);
    }

    /// <summary>
    /// Extracts compilation capabilities from a compilation.
    /// </summary>
    /// <param name="compilation">The compilation to analyze.</param>
    /// <returns>The compilation capabilities.</returns>
    public static CompilationCapabilities ExtractCompilationCapabilities(Compilation compilation)
    {
        bool hasTables = compilation.ReferencedAssemblyNames.Any(r => r.Name == "TableStorage");
        bool hasBlobs = compilation.ReferencedAssemblyNames.Any(r => r.Name == "TableStorage.Blobs");

        return new CompilationCapabilities(hasTables, hasBlobs);
    }    /// <summary>
    /// Extracts generation options from analyzer config options.
    /// </summary>
    /// <param name="optionsProvider">The analyzer config options provider.</param>
    /// <returns>The generation options.</returns>
    public static GenerationOptions ExtractGenerationOptions(Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider optionsProvider)
    {
        bool publishAot = ConfigurationHelper.GetPublishAotProperty(optionsProvider);
        string? tableStorageSerializerContext = ConfigurationHelper.GetTableStorageSerializerContextProperty(optionsProvider);

        return new GenerationOptions(publishAot, tableStorageSerializerContext);
    }

    private static TableContextMemberInfo? ExtractTableContextMemberInfo(IPropertySymbol property)
    {
        // Extract member information based on the property symbol
        string name = property.Name;
        string type = property.Type.ToDisplayString();
        string typeKind = property.Type.TypeKind.ToString();
        
        // Determine set type based on the property type
        string setType = DetermineSetType(property.Type);

        return new TableContextMemberInfo(name, type, typeKind, setType);
    }

    private static string DetermineSetType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType)
        {
            if (namedType.Name.Contains("TableSet"))
            {
                return "TableSet";
            }
            
            if (namedType.Name.Contains("BlobSet"))
            {
                return "BlobSet";
            }
        }

        return "Unknown";
    }

    private static bool IsTableContextMember(IPropertySymbol property)
    {
        // Check if this property should be included in the table context generation
        var type = property.Type;
        return type is INamedTypeSymbol namedType && 
               (namedType.Name.Contains("TableSet") || namedType.Name.Contains("BlobSet"));
    }

    private static AttributeData? GetTableSetAttribute(INamedTypeSymbol classSymbol)
    {
        return classSymbol.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.Name == "TableSetAttribute");
    }

    private static T GetAttributeProperty<T>(AttributeData attribute, string propertyName)
    {
        var namedArgument = attribute.NamedArguments
            .FirstOrDefault(arg => arg.Key == propertyName);
        
        if (namedArgument.Value.Value is T value)
        {
            return value;
        }

        return default!;
    }

    private static void ExtractTableSetMembers(
        INamedTypeSymbol classSymbol, 
        AttributeData tableSetAttribute,
        List<TableSetMemberInfo> members, 
        List<TableSetPrettyMemberInfo> prettyMembers)
    {
        // Extract partition key and row key from attribute
        string partitionKey = GetAttributeProperty<string>(tableSetAttribute, "PartitionKey") ?? "PartitionKey";
        string rowKey = GetAttributeProperty<string>(tableSetAttribute, "RowKey") ?? "RowKey";
        bool trackChanges = GetAttributeProperty<bool>(tableSetAttribute, "TrackChanges");

        // Extract property attributes
        foreach (var propertyAttribute in GetTableSetPropertyAttributes(classSymbol))
        {
            if (propertyAttribute.ConstructorArguments.Length >= 2)
            {
                var typeArg = propertyAttribute.ConstructorArguments[0];
                var nameArg = propertyAttribute.ConstructorArguments[1];

                if (typeArg.Value is INamedTypeSymbol memberType && nameArg.Value is string memberName)
                {
                    bool generateProperty = true; // This would need more logic
                    bool isPartial = false; // This would need more logic
                    bool tagBlob = GetAttributeProperty<bool>(propertyAttribute, "Tag");

                    var memberInfo = new TableSetMemberInfo(
                        memberName,
                        memberType.ToDisplayString(),
                        memberType.TypeKind.ToString(),
                        generateProperty,
                        partitionKey,
                        rowKey,
                        trackChanges,
                        isPartial,
                        tagBlob);                    members.Add(memberInfo);

                    // Add pretty member if needed
                    if (memberName != memberType.Name)
                    {
                        prettyMembers.Add(new TableSetPrettyMemberInfo(memberType.Name, memberName));
                    }
                }
            }
        }
    }

    private static IEnumerable<AttributeData> GetTableSetPropertyAttributes(INamedTypeSymbol classSymbol)
    {
        return classSymbol.GetAttributes()
            .Where(attr => attr.AttributeClass?.Name == "TableSetPropertyAttribute");
    }
}
