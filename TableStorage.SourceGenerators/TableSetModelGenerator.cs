using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;

namespace TableStorage.SourceGenerators;

[Generator]
public class TableSetModelGenerator : IIncrementalGenerator
{
    private const string TableAttributes = Header.Value + @"using System;

namespace TableStorage
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class TableSetAttribute : Attribute
    {
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
#if TABLESTORAGE
        public bool TrackChanges { get; set; }
        public bool DisableTables { get; set; }
#endif
#if TABLESTORAGE_BLOBS
        public bool SupportBlobs { get; set; }
#endif
    }


    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class TableSetPropertyAttribute : Attribute
    {
        public TableSetPropertyAttribute(Type type, string name)
        {
        }

#if TABLESTORAGE_BLOBS
        public bool Tag { get; set; }
#endif
    }


#if TABLESTORAGE_BLOBS
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class TagAttribute : Attribute
    {
    }
#endif
}";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx => ctx.AddSource("TableSetAttributes.g.cs", SourceText.From(TableAttributes, Encoding.UTF8)));

        IncrementalValueProvider<bool> publishAotProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (optionsProvider, _) => GetPublishAotProperty(optionsProvider));

        IncrementalValueProvider<string?> tableStorageSerializerContextProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (optionsProvider, _) => GetTableStorageSerializerContextProperty(optionsProvider));

        IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "TableStorage.TableSetAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax classSyntax && classSyntax.AttributeLists.Count > 0,
                transform: static (context, _) => (ClassDeclarationSyntax)context.TargetNode)
            .WithTrackingName("TableSetAttributeClasses");

        IncrementalValueProvider<(Compilation Compilation, ImmutableArray<ClassDeclarationSyntax> Classes)> compilationAndClasses =
            context.CompilationProvider.Combine(classDeclarations.Collect());

        IncrementalValueProvider<((Compilation Compilation, ImmutableArray<ClassDeclarationSyntax> Classes) Left, (bool PublishAot, string? TableStorageSerializerContext) Right)> combinedProviders =
            compilationAndClasses.Combine(publishAotProvider.Combine(tableStorageSerializerContextProvider));

        context.RegisterSourceOutput(combinedProviders,
            static (spc, source) => Execute(
                source.Left.Compilation,
                source.Left.Classes,
                source.Right.PublishAot,
                source.Right.TableStorageSerializerContext,
                spc));
    }

    private static bool GetPublishAotProperty(AnalyzerConfigOptionsProvider optionsProvider)
    {
        return optionsProvider.GlobalOptions.TryGetValue("build_property.PublishAot", out string? publishAotValue) &&
               bool.TryParse(publishAotValue, out bool parsedPublishAot) &&
               parsedPublishAot;
    }

    private static string? GetTableStorageSerializerContextProperty(AnalyzerConfigOptionsProvider optionsProvider)
    {
        return optionsProvider.GlobalOptions.TryGetValue("build_property.TableStorageSerializerContext", out string? serializerContextValue)
            ? serializerContextValue
            : null;
    }

    private static bool AreRequiredAssembliesReferenced(Compilation compilation, SourceProductionContext context)
    {
        bool hasTableStorage = compilation.ReferencedAssemblyNames.Any(asm => asm.Name == "TableStorage");
        bool hasTableStorageBlobs = compilation.ReferencedAssemblyNames.Any(asm => asm.Name == "TableStorage.Blobs");

        if (!hasTableStorage && !hasTableStorageBlobs)
        {
            var descriptor = new DiagnosticDescriptor(
                id: "TSG001",
                title: "Missing TableStorage Reference",
                messageFormat: "The TableStorage or TableStorage.Blobs assembly reference is required for TableContext generation.",
                category: "TableStorage.SourceGenerators",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true
            );
            context.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None));
            return false;
        }

        return true;
    }

    private static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, bool publishAot, string? tableStorageSerializerContext, SourceProductionContext context)
    {
        if (!AreRequiredAssembliesReferenced(compilation, context))
        {
            return;
        }

        if (classes.IsDefaultOrEmpty)
        {
            // nothing to do yet
            return;
        }

        // Convert each ClassDeclarationSyntax to a ClassToGenerate
        List<ClassToGenerate> classesToGenerate = GetTypesToGenerate(compilation, classes.Distinct(), context.CancellationToken);

        // If there were errors in the ClassDeclarationSyntax, we won't create an
        // ClassToGenerate for it, so make sure we have something to generate
        if (classesToGenerate.Count > 0)
        {
            // generate the source code and add it to the output
            foreach ((string name, string modelResult) in GenerateTableContextClasses(classesToGenerate, publishAot, tableStorageSerializerContext))
            {
                context.AddSource(name + ".g.cs", SourceText.From(modelResult, Encoding.UTF8));
            }
        }
    }

    private static ClassToGenerate? ProcessClassDeclaration(Compilation compilation, ClassDeclarationSyntax classDeclarationSyntax, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        SemanticModel semanticModel = compilation.GetSemanticModel(classDeclarationSyntax.SyntaxTree);
        if (semanticModel.GetDeclaredSymbol(classDeclarationSyntax, cancellationToken: ct) is not INamedTypeSymbol classSymbol)
        {
            return null; // something went wrong, bail out
        }

        var relevantAttributes = GetRelevantAttributes(classDeclarationSyntax, semanticModel, ct);
        AttributeSyntax? tableSetAttributeSyntax = relevantAttributes.FirstOrDefault(attr => attr.fullName == "TableStorage.TableSetAttribute").attributeSyntax;

        if (tableSetAttributeSyntax == null)
        {
            // This should not happen if ForAttributeWithMetadataName is working correctly,
            // but as a safeguard or if the attribute is malformed.
            return null;
        }

        var (partitionKeyProxy, rowKeyProxy, prettyMembers) = ProcessTableSetAttributeArguments(tableSetAttributeSyntax);
        bool withBlobSupport = GetArgumentValue(tableSetAttributeSyntax, "SupportBlobs") == "true";
        bool withTablesSupport = GetArgumentValue(tableSetAttributeSyntax, "DisableTables") != "true";
        bool withChangeTracking = withTablesSupport && GetArgumentValue(tableSetAttributeSyntax, "TrackChanges") == "true";

        List<MemberToGenerate> members = ProcessClassMembers(classSymbol, prettyMembers, withChangeTracking, partitionKeyProxy ?? "null", rowKeyProxy ?? "null", ct);
        ProcessTableSetPropertyAttributes(relevantAttributes, semanticModel, members, withChangeTracking, partitionKeyProxy ?? "null", rowKeyProxy ?? "null", ct);

        return new ClassToGenerate(classSymbol.Name, classSymbol.ContainingNamespace.ToDisplayString(), members, prettyMembers, withBlobSupport, withTablesSupport);
    }

    private static List<(string fullName, AttributeSyntax attributeSyntax)> GetRelevantAttributes(ClassDeclarationSyntax classDeclarationSyntax, SemanticModel semanticModel, CancellationToken ct)
    {
        List<(string fullName, AttributeSyntax attributeSyntax)> relevantSymbols = [];
        foreach (AttributeListSyntax attributeListSyntax in classDeclarationSyntax.AttributeLists)
        {
            foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
            {
                ct.ThrowIfCancellationRequested();
                if (semanticModel.GetSymbolInfo(attributeSyntax, cancellationToken: ct).Symbol is not IMethodSymbol attributeSymbol)
                {
                    continue; // weird, we couldn't get the symbol, ignore it
                }

                INamedTypeSymbol attributeContainingTypeSymbol = attributeSymbol.ContainingType;
                string fullName = attributeContainingTypeSymbol.ToDisplayString();

                if (fullName.StartsWith("TableStorage."))
                {
                    relevantSymbols.Add((fullName, attributeSyntax));
                }
            }
        }

        return relevantSymbols;
    }

    private static (string? PartitionKeyProxy, string? RowKeyProxy, List<PrettyMemberToGenerate> PrettyMembers) ProcessTableSetAttributeArguments(AttributeSyntax tablesetAttribute)
    {
        List<PrettyMemberToGenerate> prettyMembers = new(2);
        string? partitionKeyProxy = GetArgumentValue(tablesetAttribute, "PartitionKey");
        if (partitionKeyProxy != null)
        {
            prettyMembers.Add(new(partitionKeyProxy, "PartitionKey"));
        }

        string? rowKeyProxy = GetArgumentValue(tablesetAttribute, "RowKey");
        if (rowKeyProxy != null)
        {
            prettyMembers.Add(new(rowKeyProxy, "RowKey"));
        }

        return (partitionKeyProxy, rowKeyProxy, prettyMembers);
    }

    private static List<MemberToGenerate> ProcessClassMembers(INamedTypeSymbol classSymbol, List<PrettyMemberToGenerate> prettyMembers, bool withChangeTracking, string partitionKeyForNewMembers, string rowKeyForNewMembers, CancellationToken ct)
    {
        ImmutableArray<ISymbol> classMembersSymbols = classSymbol.GetMembers();
        List<MemberToGenerate> members = new(classMembersSymbols.Length);

        foreach (ISymbol memberSymbol in classMembersSymbols)
        {
            ct.ThrowIfCancellationRequested();
            if (memberSymbol is IPropertySymbol property)
            {
                switch (property.Name)
                {
                    case "PartitionKey":
                    case "RowKey":
                    case "Timestamp":
                    case "ETag":
                    case "this[]": // Indexer
                    case "Keys":   // From IDictionary
                    case "Values": // From IDictionary
                    case "Count":  // From IDictionary
                    case "IsReadOnly": // From IDictionary
                        break;

                    default:
                        ITypeSymbol type = property.Type;
                        TypeKind typeKind = GetTypeKind(type);
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
                        break;
                }
            }
        }

        return members;
    }

    private static void ProcessTableSetPropertyAttributes(
        List<(string fullName, AttributeSyntax attributeSyntax)> relevantAttributes,
        SemanticModel semanticModel,
        List<MemberToGenerate> members,
        bool withChangeTracking,
        string partitionKeyProxy,
        string rowKeyProxy,
        CancellationToken ct)
    {
        foreach ((string attrFullName, AttributeSyntax tableSetPropertyAttribute) in relevantAttributes.Where(x => x.fullName == "TableStorage.TableSetPropertyAttribute"))
        {
            ct.ThrowIfCancellationRequested();
            if (tableSetPropertyAttribute.ArgumentList == null || tableSetPropertyAttribute.ArgumentList.Arguments.Count < 2)
            {
                continue;
            }

            var nameSyntax = tableSetPropertyAttribute.ArgumentList.Arguments[1].Expression as LiteralExpressionSyntax;
            string name = nameSyntax?.Token.ValueText ?? string.Empty;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (tableSetPropertyAttribute.ArgumentList.Arguments[0].Expression is not TypeOfExpressionSyntax typeOfSyntax)
            {
                continue;
            }

            TypeSyntax typeSyntax = typeOfSyntax.Type;
            TypeInfo typeInfo = semanticModel.GetTypeInfo(typeSyntax, cancellationToken: ct);
            string type = typeInfo.Type?.ToDisplayString() ?? typeSyntax.ToFullString();
            TypeKind typeKind = GetTypeKind(typeInfo.Type);
            bool tagBlob = GetArgumentValue(tableSetPropertyAttribute, "Tag") == "true";

            members.Add(new MemberToGenerate(
                name: name,
                type: type,
                typeKind: typeKind,
                generateProperty: true, // These are always generated
                partitionKeyProxy: partitionKeyProxy,
                rowKeyProxy: rowKeyProxy,
                withChangeTracking: withChangeTracking,
                isPartial: false, // These are not pre-existing partial properties
                tagBlob: tagBlob
            ));
        }
    }

    private static List<ClassToGenerate> GetTypesToGenerate(Compilation compilation, IEnumerable<ClassDeclarationSyntax> classes, CancellationToken ct)
    {
        List<ClassToGenerate> classesToGenerate = [];

        foreach (ClassDeclarationSyntax classDeclarationSyntax in classes)
        {
            var classToGen = ProcessClassDeclaration(compilation, classDeclarationSyntax, ct);
            if (classToGen != null)
            {
                classesToGenerate.Add(classToGen.Value);
            }
        }

        return classesToGenerate;
    }

    private static string? GetArgumentValue(AttributeSyntax tablesetAttribute, string name)
    {
        string? result = tablesetAttribute.ArgumentList?.Arguments.Where(x => x.NameEquals?.Name.NormalizeWhitespace().ToFullString() == name)
                                                                  .Select(x => x.Expression.NormalizeWhitespace().ToFullString())
                                                                  .FirstOrDefault();

        if (!string.IsNullOrEmpty(result))
        {
            result = Regex.Replace(result, @"nameof\s*\(\s*([^\s)]+)\s*\)", "$1").Trim('"');
        }

        return result;
    }

    private static TypeKind GetTypeKind(ITypeSymbol? type) => type switch
    {
        null => TypeKind.Unknown,
        INamedTypeSymbol namedTypeSymbol when type.NullableAnnotation == NullableAnnotation.Annotated || //Sometimes it's nullable yet not annoted
                                              namedTypeSymbol.ConstructedFrom.ToDisplayString() == "System.Nullable<T>" => namedTypeSymbol.TypeArguments.Length is not 0
                                                                                                                           ? namedTypeSymbol.TypeArguments[0].TypeKind
                                                                                                                           : namedTypeSymbol.ConstructedFrom.TypeKind,
        _ => type.TypeKind,
    };

    private static string GenerateSingleTableSetClassString(ClassToGenerate classToGenerate, bool publishAot, string? tableStorageSerializerContext)
    {
        StringBuilder modelBuilder = new();

        modelBuilder.Append(Header.Value).Append(@"using Microsoft.Extensions.DependencyInjection;
using TableStorage;
using System.Collections.Generic;
using System;
");

        if (classToGenerate.Members.Any(m => m.WithChangeTracking))
        {
            modelBuilder.AppendLine("using System.Linq;");
        }

        if (classToGenerate.WithBlobSupport)
        {
            modelBuilder.AppendLine("using System.Text.Json;");
        }

        GenerateModel(modelBuilder, classToGenerate, publishAot, tableStorageSerializerContext);

        return modelBuilder.ToString();
    }

    public static IEnumerable<(string name, string result)> GenerateTableContextClasses(List<ClassToGenerate> classesToGenerate, bool publishAot, string? tableStorageSerializerContext)
    {
        foreach (ClassToGenerate classToGenerate in classesToGenerate)
        {
            string modelResult = GenerateSingleTableSetClassString(classToGenerate, publishAot, tableStorageSerializerContext);
            yield return (classToGenerate.Namespace + "." + classToGenerate.Name, modelResult);
        }
    }

    // Model generation methods
    private static void GenerateModel(StringBuilder sb, ClassToGenerate classToGenerate, bool publishAot, string? tableStorageSerializerContext)
    {
        var modelContext = InitializeModelContext(classToGenerate);
        
        GenerateNamespaceStart(sb, classToGenerate.Namespace);
        GenerateClassSignature(sb, classToGenerate, modelContext);
        
        if (classToGenerate.WithTablesSupport)
        {
            GenerateTableSetFactoryMethod(sb, classToGenerate, modelContext);
        }
        
        if (classToGenerate.WithBlobSupport)
        {
            GenerateBlobSupportMembers(sb, classToGenerate, modelContext);
        }
        
        if (classToGenerate.WithTablesSupport && modelContext.HasChangeTracking)
        {
            GenerateChangeTrackingSupport(sb, classToGenerate, modelContext);
        }
        
        if (classToGenerate.WithTablesSupport)
        {
            GenerateTableEntityMembers(sb, modelContext);
        }
        
        GenerateClassProperties(sb, classToGenerate);
        GenerateIndexerImplementation(sb, classToGenerate, modelContext, publishAot, tableStorageSerializerContext);
        
        if (classToGenerate.WithTablesSupport)
        {
            GenerateDictionaryImplementation(sb, classToGenerate, modelContext);
        }
        
        sb.Append(@"
    }
");
        
        GenerateNamespaceEnd(sb, classToGenerate.Namespace);
    }

    private static ModelContext InitializeModelContext(ClassToGenerate classToGenerate)
    {
        bool hasChangeTracking = classToGenerate.Members.Any(x => x.WithChangeTracking);
        bool hasPartitionKeyProxy = classToGenerate.TryGetPrettyMember("PartitionKey", out PrettyMemberToGenerate partitionKeyProxy);
        string realPartitionKey = hasPartitionKeyProxy ? partitionKeyProxy.Name : "PartitionKey";
        bool hasRowKeyProxy = classToGenerate.TryGetPrettyMember("RowKey", out PrettyMemberToGenerate rowKeyProxy);
        string realRowKey = hasRowKeyProxy ? rowKeyProxy.Name : "RowKey";

        return new ModelContext(
            hasChangeTracking,
            hasPartitionKeyProxy,
            hasRowKeyProxy,
            partitionKeyProxy,
            rowKeyProxy,
            realPartitionKey,
            realRowKey
        );
    }

    private static void GenerateNamespaceStart(StringBuilder sb, string? @namespace)
    {
        if (!string.IsNullOrEmpty(@namespace))
        {
            sb.Append(@"
namespace ").Append(@namespace).Append(@"
{");
        }
    }

    private static void GenerateNamespaceEnd(StringBuilder sb, string? @namespace)
    {
        if (!string.IsNullOrEmpty(@namespace))
        {
            sb.Append('}');
        }
    }

    private static void GenerateClassSignature(StringBuilder sb, ClassToGenerate classToGenerate, in ModelContext context)
    {
        sb.Append(@"
    [System.Diagnostics.DebuggerDisplay(@""").Append(classToGenerate.Name).Append(@" \{ {").Append(context.RealPartitionKey).Append("}, {").Append(context.RealRowKey).Append(@"} \}"")]
    partial class ").Append(classToGenerate.Name);

        if (classToGenerate.WithTablesSupport || classToGenerate.WithBlobSupport)
        {
            sb.Append(" : ");
        }

        if (classToGenerate.WithTablesSupport)
        {
            sb.Append(@"IDictionary<string, object>, Azure.Data.Tables.ITableEntity");

            if (context.HasChangeTracking)
            {
                sb.Append(", TableStorage.IChangeTracking");
            }
        }

        if (classToGenerate.WithBlobSupport)
        {
            if (classToGenerate.WithTablesSupport)
            {
                sb.Append(", ");
            }

            sb.Append("TableStorage.IBlobEntity");
        }

        sb.Append(@"
    {
");
    }

    private static void GenerateTableSetFactoryMethod(StringBuilder sb, ClassToGenerate classToGenerate, in ModelContext context)
    {
        sb.Append(@"
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static TableSet<").Append(classToGenerate.Name).Append(@"> CreateTableSet(TableStorage.ICreator creator, string name)
        {
            return creator.CreateSet");

        if (context.HasChangeTracking)
        {
            sb.Append("WithChangeTracking");
        }

        sb.Append('<').Append(classToGenerate.Name).Append(">(name, ");

        if (context.HasPartitionKeyProxy)
        {
            sb.Append('"').Append(context.PartitionKeyProxy.Name).Append('"');
        }
        else
        {
            sb.Append("null");
        }

        sb.Append(", ");

        if (context.HasRowKeyProxy)
        {
            sb.Append('"').Append(context.RowKeyProxy.Name).Append('"');
        }
        else
        {
            sb.Append("null");
        }

        sb.Append(@");
        }
");
    }

    private static void GenerateBlobSupportMembers(StringBuilder sb, ClassToGenerate classToGenerate, in ModelContext context)
    {
        // Generate PartitionKey and RowKey implementation for IBlobEntity
        if (context.HasPartitionKeyProxy)
        {
            sb.Append(@"
        string IBlobEntity.PartitionKey => ").Append(context.RealPartitionKey).Append(';');
        }
        else if (!classToGenerate.WithTablesSupport)
        {
            sb.Append(@"
        public string PartitionKey { get; set; }");
        }

        if (context.HasRowKeyProxy)
        {
            sb.Append(@"
        string IBlobEntity.RowKey => ").Append(context.RealRowKey).Append(';');
        }
        else if (!classToGenerate.WithTablesSupport)
        {
            sb.Append(@"
        public string RowKey { get; set; }");
        }

        // Generate CreateBlobSet method
        sb.Append(@"

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static BlobSet<").Append(classToGenerate.Name).Append(@"> CreateBlobSet(TableStorage.IBlobCreator creator, string name)
        {
            return creator.CreateSet<").Append(classToGenerate.Name).Append(@">(name, ");

        if (context.HasPartitionKeyProxy)
        {
            sb.Append('"').Append(context.PartitionKeyProxy.Name).Append('"');
        }
        else
        {
            sb.Append("null");
        }

        sb.Append(", ");

        if (context.HasRowKeyProxy)
        {
            sb.Append('"').Append(context.RowKeyProxy.Name).Append('"');
        }
        else
        {
            sb.Append("null");
        }

        sb.Append(", [");

        foreach (string? tag in classToGenerate.Members.Where(x => x.TagBlob).Select(x => x.Name))
        {
            sb.Append('"').Append(tag).Append("\", ");
        }

        sb.Append(@"]);
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static AppendBlobSet<").Append(classToGenerate.Name).Append(@"> CreateAppendBlobSet(TableStorage.IBlobCreator creator, string name)
        {
            return creator.CreateAppendSet<").Append(classToGenerate.Name).Append(@">(name, ");

        if (context.HasPartitionKeyProxy)
        {
            sb.Append('"').Append(context.PartitionKeyProxy.Name).Append('"');
        }
        else
        {
            sb.Append("null");
        }

        sb.Append(", ");

        if (context.HasRowKeyProxy)
        {
            sb.Append('"').Append(context.RowKeyProxy.Name).Append('"');
        }
        else
        {
            sb.Append("null");
        }

        sb.Append(", [");

        foreach (string? tag in classToGenerate.Members.Where(x => x.TagBlob).Select(x => x.Name))
        {
            sb.Append('"').Append(tag).Append("\", ");
        }

        sb.Append(@"]);
        }
");
    }

    private static void GenerateChangeTrackingSupport(StringBuilder sb, ClassToGenerate classToGenerate, in ModelContext context)
    {
        string realPartitionKey = context.RealPartitionKey;
        string realRowKey = context.RealRowKey;

        sb.Append(@"
        private readonly HashSet<string> _changes = new HashSet<string>();

        public void AcceptChanges()
        {
            _changes.Clear();
        }

        public bool IsChanged()
        {
            return _changes.Count != 0;
        }

        public bool IsChanged(string field)
        {
            return _changes.Contains(field);
        }

        public Azure.Data.Tables.ITableEntity GetEntity()
        {
            var entityDictionary = new Dictionary<string, object>(").Append(4 + classToGenerate.Members.Count(x => x.Name != realPartitionKey && x.Name != realRowKey && !x.WithChangeTracking)).Append(@" + _changes.Count)
            {
                [""PartitionKey""] = ").Append(context.RealPartitionKey).Append(@",
                [""RowKey""] = ").Append(context.RealRowKey).Append(@",
                [""Timestamp""] = Timestamp,
                [""ETag""] = ETag.ToString(),");

        foreach (MemberToGenerate item in classToGenerate.Members.Where(x => x.Name != realPartitionKey && x.Name != realRowKey && !x.WithChangeTracking))
        {
            sb.AppendLine().Append("                [\"").Append(item.Name).Append("\"] = ");

            if (item.TypeKind == TypeKind.Enum)
            {
                sb.Append("(int");

                if (item.Type.EndsWith("?"))
                {
                    sb.Append('?');
                }

                sb.Append(") ");
            }

            sb.Append(item.Name).Append(',');
        }

        sb.Append(@"
            };

            foreach (var key in _changes)
            {
                entityDictionary[key] = key switch
                {
");

        foreach (MemberToGenerate item in classToGenerate.Members.Where(x => x.Name != realPartitionKey && x.Name != realRowKey && x.WithChangeTracking))
        {
            sb.Append("                    \"").Append(item.Name).Append("\" => ");

            if (item.TypeKind == TypeKind.Enum)
            {
                sb.Append("(int");

                if (item.Type.EndsWith("?"))
                {
                    sb.Append('?');
                }

                sb.Append(") ");
            }

            sb.Append(item.Name).AppendLine(", ");
        }

        sb.Append(@"                    _ => throw new System.ArgumentException()
                };");

        sb.Append(@"
            }

            return new Azure.Data.Tables.TableEntity(entityDictionary);
        }

        public void SetChanged(string field)
        {
            _changes.Add(field);
        }

        public void SetChanged()
        {");

        foreach (MemberToGenerate member in classToGenerate.Members.Where(x => x.Name != realPartitionKey && x.Name != realRowKey && x.GenerateProperty))
        {
            sb.AppendLine().Append("            SetChanged(\"" + member.Name + "\");");
        }

        sb.Append(@"
        }
");
    }

    private static void GenerateTableEntityMembers(StringBuilder sb, in ModelContext context)
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

    private static void GenerateClassProperties(StringBuilder sb, ClassToGenerate classToGenerate)
    {
        // Generate custom properties
        foreach (MemberToGenerate item in classToGenerate.Members.Where(x => x.GenerateProperty))
        {
            if (classToGenerate.WithTablesSupport)
            {
                sb.Append(@"
        [System.Runtime.Serialization.IgnoreDataMember]");
            }

            sb.Append(@"
        public ");

            if (item.IsPartial)
            {
                sb.Append("partial ");
            }

            sb.Append(item.Type).Append(' ').Append(item.Name);

            if (item.IsPartial || item.WithChangeTracking)
            {
                sb.Append(@"
        { 
            get
            {
                return _").Append(item.Name).Append(@";
            }
            set
            {
                _").Append(item.Name).Append(@" = value;");

                if (item.WithChangeTracking)
                {
                    sb.Append(@"
                SetChanged(""").Append(item.Name).Append(@""");");
                }

                sb.Append(@"
            }
        }
        private ").Append(item.Type).Append(" _").Append(item.Name).Append(';');
            }
            else
            {
                sb.Append(" { get; set; }");
            }
        }

        // Generate pretty member properties
        foreach (PrettyMemberToGenerate item in classToGenerate.PrettyMembers)
        {
            bool partial = classToGenerate.Members.Any(x => x.IsPartial && x.Name == item.Name);
            if (item.Proxy is "PartitionKey" or "RowKey")
            {
                if (classToGenerate.WithTablesSupport)
                {
                    sb.Append(@"
        [System.Runtime.Serialization.IgnoreDataMember]");
                }

                sb.Append(@"
        public ");

                if (partial)
                {
                    sb.Append("partial ");
                }

                sb.Append("string ").Append(item.Name);

                if (partial)
                {
                    sb.Append(" { get => _").Append(item.Name).Append("; set => _").Append(item.Name).Append(" = value; }")
                      .Append("        private string _").Append(item.Name).Append(';');
                }
                else
                {
                    sb.Append(" { get; set; }");
                }
            }
            else
            {
                if (classToGenerate.WithTablesSupport)
                {
                    sb.Append(@"
        [System.Runtime.Serialization.IgnoreDataMember]");
                }

                sb.Append(@"
        public ");

                if (partial)
                {
                    sb.Append("partial ");
                }

                sb.Append("string ").Append(item.Name).Append(" { get => ").Append(item.Proxy).Append("; set => ").Append(item.Proxy).Append(" = value; }");
            }
        }
    }

    private static void GenerateIndexerImplementation(StringBuilder sb, ClassToGenerate classToGenerate, in ModelContext context, bool publishAot, string? tableStorageSerializerContext)
    {
        sb.Append(@"

        public object this[string key]
        {
            get
            {
                switch (key)
                {
                    case ""PartitionKey"": return ").Append(context.RealPartitionKey).Append(@";
                    case ""RowKey"": return ").Append(context.RealRowKey).Append(';');

        if (classToGenerate.WithTablesSupport)
        {
            sb.Append(@"
                    case ""Timestamp"": return Timestamp;
                    case ""odata.etag"": return ETag.ToString();");
        }

        foreach (MemberToGenerate item in classToGenerate.Members)
        {
            sb.Append(@"
                    case """).Append(item.Name).Append(@""": return ").Append(item.Name).Append(';');
        }

        sb.Append(@"
                    default: return null;
                }
            }
");

        if (!classToGenerate.WithTablesSupport)
        {
            sb.AppendLine("        }");
            return;
        }

        // Generate indexer setter for table support
        sb.Append(@"

            set
            {
                switch (key)
                {
                    case ""PartitionKey"": ").Append(context.RealPartitionKey).Append(@" = value?.ToString(); break;
                    case ""RowKey"": ").Append(context.RealRowKey).Append(@" = value?.ToString(); break;
                    case ""Timestamp"": Timestamp = ");

        if (classToGenerate.WithBlobSupport)
        {
            sb.Append("(value is System.Text.Json.JsonElement _TimestampJsonElement ? _TimestampJsonElement.GetDateTimeOffset() : (System.DateTimeOffset?)value)");
        }
        else
        {
            sb.Append("(System.DateTimeOffset?)value");
        }

        sb.Append(@"; break;
                    case ""odata.etag"": ETag = new Azure.ETag(value?.ToString()); break;");

        foreach (MemberToGenerate item in classToGenerate.Members)
        {
            sb.Append(@"
                    case """).Append(item.Name).Append(@""": ");

            if (item.WithChangeTracking)
            {
                sb.Append('_');
            }

            sb.Append(item.Name).Append(" = ");

            GenerateValueConversion(sb, item, classToGenerate.WithBlobSupport, publishAot, tableStorageSerializerContext);

            sb.Append("; break;");
        }

        sb.Append(@"
                }
            }
        }");
    }

    private static void GenerateValueConversion(StringBuilder sb, MemberToGenerate item, bool withBlobSupport, bool publishAot, string? tableStorageSerializerContext)
    {
        // Begin cast
        sb.Append('(');

        if (item.Type == typeof(DateTime).FullName)
        {
            if (withBlobSupport)
            {
                sb.Append("value is System.Text.Json.JsonElement _").Append(item.Name).Append("JsonElement ? _").Append(item.Name).Append("JsonElement.GetDateTimeOffset() : (DateTimeOffset)value).DateTime");
            }
            else
            {
                sb.Append("(DateTimeOffset)value).DateTime");
            }
        }
        else if (item.Type == typeof(DateTime).FullName + "?")
        {
            if (withBlobSupport)
            {
                sb.Append("value is System.Text.Json.JsonElement _").Append(item.Name).Append("JsonElement ? _").Append(item.Name).Append("JsonElement.GetDateTimeOffset() : value as DateTimeOffset?)?.DateTime");
            }
            else
            {
                sb.Append("value as DateTimeOffset?)?.DateTime");
            }
        }
        else if (item.TypeKind == TypeKind.Enum)
        {
            GenerateEnumConversion(sb, item, withBlobSupport);
        }
        else
        {
            if (withBlobSupport)
            {
                GenerateBlobJsonConversion(sb, item, publishAot, tableStorageSerializerContext);
            }
            else
            {
                sb.Append(item.Type).Append(") value");
            }
        }
    }

    private static void GenerateEnumConversion(StringBuilder sb, MemberToGenerate item, bool withBlobSupport)
    {
        if (withBlobSupport)
        {
            sb.Append("value is System.Text.Json.JsonElement _")
                .Append(item.Name)
                .Append("JsonElement ? (Enum.TryParse(_")
                .Append(item.Name)
                .Append("JsonElement.ToString(), out ")
                .Append(item.Type.TrimEnd('?'))
                .Append(" _")
                .Append(item.Name)
                .Append("JsonElementParseResult) ? _")
                .Append(item.Name)
                .Append("JsonElementParseResult : default(")
                .Append(item.Type)
                .Append(")) : (");
        }

        sb.Append("value is int _").Append(item.Name).Append("Integer ? (").Append(item.Type).Append(") _").Append(item.Name).Append("Integer : ")
            .Append("Enum.TryParse(value?.ToString(), out ")
            .Append(item.Type.TrimEnd('?'))
            .Append(" _")
            .Append(item.Name)
            .Append("ParseResult) ? _")
            .Append(item.Name)
            .Append("ParseResult : default(")
            .Append(item.Type)
            .Append("))");

        if (withBlobSupport)
        {
            sb.Append(')');
        }
    }

    private static void GenerateBlobJsonConversion(StringBuilder sb, MemberToGenerate item, bool publishAot, string? tableStorageSerializerContext)
    {
        string? deserializing = item.Type.ToLowerInvariant().TrimEnd('?') switch
        {
            "string" or "system.string" => "GetString(",
            "int" or "system.int32" => "GetInt32(",
            "long" or "system.int64" => "GetInt64(",
            "double" or "system.double" => "GetDouble(",
            "float" or "system.single" => "GetSingle(",
            "decimal" or "system.decimal" => "GetDecimal(",
            "bool" or "system.boolean" => "GetBoolean(",
            "system.guid" => "GetGuid(",
            "system.datetime" => "GetDateTime(",
            "system.datetimeoffset" => "GetDateTimeOffset(",
            "system.timespan" => "GetTimeSpan(",
            _ when !publishAot && string.IsNullOrEmpty(tableStorageSerializerContext) => "Deserialize<" + item.Type + ">(",
            _ => null
        };

        if (deserializing is null)
        {
            sb.Append(item.Type)
                .Append(") ");

            if (!string.IsNullOrEmpty(tableStorageSerializerContext))
            {
                sb.Append("( value is System.Text.Json.JsonElement _")
                    .Append(item.Name)
                    .Append($"JsonElement ? (")
                    .Append(item.Type)
                    .Append(") _")
                    .Append(item.Name)
                    .Append("JsonElement.Deserialize(")
                    .Append(tableStorageSerializerContext)
                    .Append(".Default.GetTypeInfo(typeof(")
                    .Append(item.Type)
                    .Append("))) : ");
            }

            sb.Append(" value");

            if (!string.IsNullOrEmpty(tableStorageSerializerContext))
            {
                sb.Append(')');
            }
        }
        else
        {
            sb.Append("value is System.Text.Json.JsonElement _")
                .Append(item.Name)
                .Append("JsonElement ? _")
                .Append(item.Name)
                .Append("JsonElement.")
                .Append(deserializing)
                .Append(") : (")
                .Append(item.Type)
                .Append(") value)");
        }
    }

    private static void GenerateDictionaryImplementation(StringBuilder sb, ClassToGenerate classToGenerate, in ModelContext context)
    {
        string realPartitionKey = context.RealPartitionKey;
        string realRowKey = context.RealRowKey;
        List<MemberToGenerate> keysAndValuesToGenerate = [.. classToGenerate.Members.Where(x => x.Name != realPartitionKey && x.Name != realRowKey)];
        
        // Keys collection
        sb.Append(@"

        public ICollection<string> Keys => [ ""PartitionKey"", ""RowKey"", ""Timestamp"", ""odata.etag"", ");

        foreach (MemberToGenerate item in keysAndValuesToGenerate)
        {
            sb.Append('"').Append(item.Name).Append(@""", ");
        }

        // Values collection
        sb.Append(@" ];
        public ICollection<object> Values => [ ").Append(context.RealPartitionKey).Append(", ").Append(context.RealRowKey).Append(", Timestamp, ETag.ToString(), ");

        foreach (MemberToGenerate item in keysAndValuesToGenerate)
        {
            if (item.TypeKind == TypeKind.Enum)
            {
                sb.Append("(int");

                if (item.Type.EndsWith("?"))
                {
                    sb.Append('?');
                }

                sb.Append(") ");
            }

            sb.Append(item.Name).Append(", ");
        }

        // Count property
        sb.Append(@" ];
        public int Count => ").Append(4 + keysAndValuesToGenerate.Count).Append(@";
        public bool IsReadOnly => false;");

        // Dictionary methods
        GenerateDictionaryMethods(sb, classToGenerate, context);
    }

    private static void GenerateDictionaryMethods(StringBuilder sb, ClassToGenerate classToGenerate, ModelContext context)
    {
        bool hasChangeTracking = context.HasChangeTracking;
        
        // Add methods
        sb.Append(@"

        public void Add(string key, object value)
        {
            this[key] = value;");

        if (hasChangeTracking)
        {
            sb.Append(@"
            SetChanged(key);");
        }

        sb.Append(@"
        }

        public void Add(KeyValuePair<string, object> item)
        {
            this[item.Key] = item.Value;");

        if (hasChangeTracking)
        {
            sb.Append(@"
            SetChanged(item.Key);");
        }

        sb.Append(@"
        }

        public void Clear()
        {");

        foreach (MemberToGenerate item in classToGenerate.Members.Where(x => x.Name != context.RealPartitionKey && x.Name != context.RealRowKey))
        {
            sb.Append(@"
            ").Append(item.Name).Append(" = default(").Append(item.Type).Append(");");
        }

        sb.Append(@"
        }

        public bool Contains(KeyValuePair<string, object> item)
        {
            if (TryGetValue(item.Key, out var value))
            {
                return value == item.Value;
            }

            return false;
        }

        public bool ContainsKey(string key)
        {
            switch (key)
            {
                case ""PartitionKey"":
                case ""RowKey"":
                case ""Timestamp"":
                case ""odata.etag"":");

        foreach (MemberToGenerate item in classToGenerate.Members)
        {
            sb.Append(@"
                case """).Append(item.Name).Append(@""": ");
        }

        sb.Append(@"
                    return true;
            
                default: return false;
            }
        }

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            if (array == null)
            {
                throw new System.ArgumentNullException(""array"");
            }

            if ((uint)arrayIndex > (uint)array.Length)
            {
                throw new System.IndexOutOfRangeException();
            }

            if (array.Length - arrayIndex < Count)
            {
                throw new System.ArgumentException();
            }

            foreach (var item in this)
            {
                array[arrayIndex++] = item;
            }
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            yield return new KeyValuePair<string, object>(""PartitionKey"", ").Append(context.RealPartitionKey).Append(@");
            yield return new KeyValuePair<string, object>(""RowKey"", ").Append(context.RealRowKey).Append(@");
            yield return new KeyValuePair<string, object>(""Timestamp"", Timestamp);
            yield return new KeyValuePair<string, object>(""odata.etag"", ETag.ToString());");

        foreach (MemberToGenerate item in classToGenerate.Members.Where(x => x.Name != context.RealPartitionKey && x.Name != context.RealRowKey))
        {
            sb.Append(@"
            yield return new KeyValuePair<string, object>(""").Append(item.Name).Append(@""", ");

            if (item.TypeKind == TypeKind.Enum)
            {
                sb.Append("(int");

                if (item.Type.EndsWith("?"))
                {
                    sb.Append('?');
                }

                sb.Append(')');
            }

            sb.Append(item.Name).Append(");");
        }

        sb.Append(@"
        }

        public bool Remove(string key)
        {
            if (ContainsKey(key)) 
            {
                this[key] = null;");

        if (hasChangeTracking)
        {
            sb.Append(@"
                SetChanged(key);");
        }

        sb.Append(@"
                return true;
            }

            return false;
        }

        public bool Remove(KeyValuePair<string, object> item)
        {
            if (Contains(item)) 
            {
                this[item.Key] = null;");

        if (hasChangeTracking)
        {
            sb.Append(@"
                SetChanged(item.Key);");
        }

        sb.Append(@"
                return true;
            }

            return false;
        }

        public bool TryGetValue(string key, out object value)
        {
            switch (key)
            {
                case ""PartitionKey"": value = ").Append(context.RealPartitionKey).Append(@"; return true;
                case ""RowKey"": value = ").Append(context.RealRowKey).Append(@"; return true;
                case ""Timestamp"": value = Timestamp; return true;
                case ""odata.etag"": value = ETag; return true;");

        foreach (MemberToGenerate item in classToGenerate.Members)
        {
            sb.Append(@"
                case """).Append(item.Name).Append(@""": value = ").Append(item.Name).Append("; return true;");
        }

        sb.Append(@"
                default: value = null; return false;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }");
    }

    private readonly struct ModelContext(
        bool hasChangeTracking, 
        bool hasPartitionKeyProxy, 
        bool hasRowKeyProxy, 
        PrettyMemberToGenerate partitionKeyProxy, 
        PrettyMemberToGenerate rowKeyProxy, 
        string realPartitionKey, 
        string realRowKey)
    {
        public readonly bool HasChangeTracking = hasChangeTracking;
        public readonly bool HasPartitionKeyProxy = hasPartitionKeyProxy;
        public readonly bool HasRowKeyProxy = hasRowKeyProxy;
        public readonly PrettyMemberToGenerate PartitionKeyProxy = partitionKeyProxy; 
        public readonly PrettyMemberToGenerate RowKeyProxy = rowKeyProxy;
        public readonly string RealPartitionKey = realPartitionKey;
        public readonly string RealRowKey = realRowKey;
    }
}
