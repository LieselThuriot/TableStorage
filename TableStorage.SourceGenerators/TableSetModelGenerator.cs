using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;
using TableStorage.SourceGenerators.Generators;
using TableStorage.SourceGenerators.Generators.CodeGeneration;
using TableStorage.SourceGenerators.Models;
using TableStorage.SourceGenerators.Utilities;

namespace TableStorage.SourceGenerators;

/// <summary>
/// Source generator for creating TableSet model classes with attribute-driven configuration.
/// This generator processes classes marked with TableSetAttribute and generates the necessary
/// boilerplate code for table storage entities, blob support, and change tracking.
/// </summary>
[Generator]
public class TableSetModelGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Pre-generated source code for TableStorage attributes.
    /// </summary>
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

    /// <summary>
    /// Initializes the incremental generator by registering all the necessary providers and transformations.
    /// </summary>
    /// <param name="context">The generator initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register the attributes source
        context.RegisterPostInitializationOutput(static ctx => 
            ctx.AddSource("TableSetAttributes.g.cs", SourceText.From(TableAttributes, Encoding.UTF8)));

        // Create providers for configuration options
        IncrementalValueProvider<bool> publishAotProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (optionsProvider, _) => ConfigurationHelper.GetPublishAotProperty(optionsProvider));

        IncrementalValueProvider<string?> tableStorageSerializerContextProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (optionsProvider, _) => ConfigurationHelper.GetTableStorageSerializerContextProperty(optionsProvider));

        // Find classes with TableSetAttribute
        IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "TableStorage.TableSetAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax classSyntax && classSyntax.AttributeLists.Count > 0,
                transform: static (context, _) => (ClassDeclarationSyntax)context.TargetNode)
            .WithTrackingName("TableSetAttributeClasses");

        // Combine all providers
        IncrementalValueProvider<(Compilation Compilation, ImmutableArray<ClassDeclarationSyntax> Classes)> compilationAndClasses =
            context.CompilationProvider.Combine(classDeclarations.Collect());

        IncrementalValueProvider<((Compilation Compilation, ImmutableArray<ClassDeclarationSyntax> Classes) Left, (bool PublishAot, string? TableStorageSerializerContext) Right)> combinedProviders =
            compilationAndClasses.Combine(publishAotProvider.Combine(tableStorageSerializerContextProvider));

        // Register the main source output
        context.RegisterSourceOutput(combinedProviders,
            static (spc, source) => Execute(
                source.Left.Compilation,
                source.Left.Classes,
                source.Right.PublishAot,
                source.Right.TableStorageSerializerContext,
                spc));
    }

    /// <summary>
    /// Main execution method that processes the compilation and generates source code.
    /// </summary>
    /// <param name="compilation">The compilation being processed.</param>
    /// <param name="classes">The classes marked with TableSetAttribute.</param>
    /// <param name="publishAot">Whether AOT publishing is enabled.</param>
    /// <param name="tableStorageSerializerContext">The serializer context for AOT.</param>
    /// <param name="context">The source production context.</param>
    private static void Execute(
        Compilation compilation, 
        ImmutableArray<ClassDeclarationSyntax> classes, 
        bool publishAot, 
        string? tableStorageSerializerContext, 
        SourceProductionContext context)
    {
        // Validate that required assemblies are referenced
        if (!ValidationHelper.AreRequiredAssembliesReferenced(compilation, context))
        {
            return;
        }

        if (classes.IsDefaultOrEmpty)
        {
            return; // Nothing to generate
        }

        // Process classes to generate configuration objects
        List<ClassToGenerate> classesToGenerate = ClassProcessor.GetTypesToGenerate(
            compilation, 
            classes.Distinct(), 
            context.CancellationToken);

        // Generate source code for each valid class
        if (classesToGenerate.Count > 0)
        {
            foreach ((string name, string modelResult) in ModelGenerator.GenerateTableContextClasses(
                classesToGenerate, 
                publishAot, 
                tableStorageSerializerContext))
            {
                context.AddSource(name + ".g.cs", SourceText.From(modelResult, Encoding.UTF8));
            }
        }
    }
}
