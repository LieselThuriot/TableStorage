using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;
using TableStorage.SourceGenerators.Generators.TableContextGeneration;
using TableStorage.SourceGenerators.Models;

namespace TableStorage.SourceGenerators;

/// <summary>
/// Source generator for creating TableContext classes with dependency injection support.
/// This generator processes classes marked with TableContextAttribute and generates the necessary
/// boilerplate code for service registration, dependency injection, and table/blob set management.
/// </summary>
[Generator]
public class TableContextGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Pre-generated source code for TableContext attribute.
    /// </summary>
    private const string TableContextAttribute = Header.Value + @"using System;

namespace TableStorage
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class TableContextAttribute : Attribute
    {
    }
}";    /// <summary>
    /// Initializes the incremental generator by registering all the necessary providers and transformations.
    /// </summary>
    /// <param name="context">The generator initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register the attribute source
        context.RegisterPostInitializationOutput(ctx => 
            ctx.AddSource("TableContextAttribute.g.cs", SourceText.From(TableContextAttribute, Encoding.UTF8)));

        // Find classes with TableContextAttribute
        IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = context.SyntaxProvider
               .ForAttributeWithMetadataName(
                    "TableStorage.TableContextAttribute",
                    predicate: static (node, _) => node is ClassDeclarationSyntax,
                    transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.TargetNode
               );

        // Combine compilation and classes
        IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)> compilationAndClasses = 
            context.CompilationProvider.Combine(classDeclarations.Collect())!;
        
        // Register the main source output
        context.RegisterSourceOutput(compilationAndClasses, static (spc, source) => 
            Execute(source.Item1, source.Item2, spc));
    }    /// <summary>
    /// Main execution method that processes the compilation and generates source code.
    /// </summary>
    /// <param name="compilation">The compilation being processed.</param>
    /// <param name="classes">The classes marked with TableContextAttribute.</param>
    /// <param name="context">The source production context.</param>
    private static void Execute(
        Compilation compilation, 
        ImmutableArray<ClassDeclarationSyntax> classes, 
        SourceProductionContext context)
    {
        // Validate dependencies and get capabilities
        var capabilities = TableContextValidator.ValidateAndGetCapabilities(compilation, context);
        if (!capabilities.HasValue)
        {
            return; // Validation failed, errors already reported
        }

        if (classes.IsDefaultOrEmpty)
        {
            return; // Nothing to generate
        }

        // Process classes to generate configuration objects
        List<ContextClassToGenerate> classesToGenerate = TableContextClassProcessor.GetTypesToGenerate(
            compilation, 
            classes.Distinct(), 
            context.CancellationToken);

        // Generate source code for each valid class
        if (classesToGenerate.Count > 0)
        {
            foreach ((string name, string result) in Generators.TableContextGeneration.TableContextGenerator.GenerateTableContextClasses(
                classesToGenerate, 
                capabilities.Value.HasTables, 
                capabilities.Value.HasBlobs))
            {
                context.AddSource(name + ".g.cs", SourceText.From(result, Encoding.UTF8));
            }
        }
    }
}
