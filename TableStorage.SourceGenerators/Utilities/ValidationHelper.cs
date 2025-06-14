using Microsoft.CodeAnalysis;

namespace TableStorage.SourceGenerators.Utilities;

/// <summary>
/// Utility class for validating compilation requirements and dependencies.
/// </summary>
internal static class ValidationHelper
{
    /// <summary>
    /// Validates that the required assemblies are referenced in the compilation.
    /// </summary>
    /// <param name="compilation">The compilation to check.</param>
    /// <param name="context">The source production context for reporting diagnostics.</param>
    /// <returns>True if all required assemblies are referenced, false otherwise.</returns>
    public static bool AreRequiredAssembliesReferenced(Compilation compilation, SourceProductionContext context)
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
}
