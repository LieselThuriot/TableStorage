using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TableStorage.SourceGenerators.Generators.TableSetGeneration.AttributeProcessing;
using TableStorage.SourceGenerators.Models;

namespace TableStorage.SourceGenerators.Generators.TableSetGeneration;

/// <summary>
/// Processes class declarations to extract configuration and generate ClassToGenerate instances.
/// </summary>
internal static class ClassProcessor
{
    /// <summary>
    /// Processes a single class declaration to create a ClassToGenerate instance.
    /// </summary>
    /// <param name="compilation">The compilation context.</param>
    /// <param name="classDeclarationSyntax">The class declaration to process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A ClassToGenerate instance, or null if processing failed.</returns>
    public static ClassToGenerate? ProcessClassDeclaration(
        Compilation compilation, 
        ClassDeclarationSyntax classDeclarationSyntax, 
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        SemanticModel semanticModel = compilation.GetSemanticModel(classDeclarationSyntax.SyntaxTree);
        if (semanticModel.GetDeclaredSymbol(classDeclarationSyntax, cancellationToken: ct) is not INamedTypeSymbol classSymbol)
        {
            return null; // something went wrong, bail out
        }

        var relevantAttributes = AttributeProcessor.GetRelevantAttributes(classDeclarationSyntax, semanticModel, ct);
        AttributeSyntax? tableSetAttributeSyntax = relevantAttributes
            .FirstOrDefault(attr => attr.fullName == "TableStorage.TableSetAttribute").attributeSyntax;

        if (tableSetAttributeSyntax == null)
        {
            // This should not happen if ForAttributeWithMetadataName is working correctly,
            // but as a safeguard or if the attribute is malformed.
            return null;
        }

        var (partitionKeyProxy, rowKeyProxy, prettyMembers) = AttributeProcessor.ProcessTableSetAttributeArguments(tableSetAttributeSyntax);
        
        bool withBlobSupport = AttributeProcessor.GetArgumentValue(tableSetAttributeSyntax, "SupportBlobs") == "true";
        bool withTablesSupport = AttributeProcessor.GetArgumentValue(tableSetAttributeSyntax, "DisableTables") != "true";
        bool withChangeTracking = withTablesSupport && AttributeProcessor.GetArgumentValue(tableSetAttributeSyntax, "TrackChanges") == "true";

        List<MemberToGenerate> members = MemberProcessor.ProcessClassMembers(
            classSymbol, 
            prettyMembers, 
            withChangeTracking, 
            partitionKeyProxy ?? "null", 
            rowKeyProxy ?? "null", 
            ct);
            
        AttributeProcessor.ProcessTableSetPropertyAttributes(
            relevantAttributes, 
            semanticModel, 
            members, 
            withChangeTracking, 
            partitionKeyProxy ?? "null", 
            rowKeyProxy ?? "null", 
            ct);

        return new ClassToGenerate(
            classSymbol.Name, 
            classSymbol.ContainingNamespace.ToDisplayString(), 
            members, 
            prettyMembers, 
            withBlobSupport, 
            withTablesSupport);
    }

    /// <summary>
    /// Processes multiple class declarations to generate a list of ClassToGenerate instances.
    /// </summary>
    /// <param name="compilation">The compilation context.</param>
    /// <param name="classes">The class declarations to process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of successfully processed classes.</returns>
    public static List<ClassToGenerate> GetTypesToGenerate(
        Compilation compilation, 
        IEnumerable<ClassDeclarationSyntax> classes, 
        CancellationToken ct)
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
}
