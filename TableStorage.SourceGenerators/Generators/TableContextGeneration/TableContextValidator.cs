using Microsoft.CodeAnalysis;

namespace TableStorage.SourceGenerators.Generators.TableContextGeneration;

/// <summary>
/// Validates dependencies and requirements for table context generation.
/// </summary>
internal static class TableContextValidator
{
    /// <summary>
    /// Represents the capabilities available based on referenced assemblies.
    /// </summary>
    public readonly struct TableContextCapabilities(bool hasTables, bool hasBlobs)
    {
        public readonly bool HasTables = hasTables;
        public readonly bool HasBlobs = hasBlobs;

        /// <summary>
        /// Indicates whether any table storage capabilities are available.
        /// </summary>
        public bool HasAnyCapabilities => HasTables || HasBlobs;
    }

    /// <summary>
    /// Validates that required assemblies are referenced and determines available capabilities.
    /// </summary>
    /// <param name="compilation">The compilation to check.</param>
    /// <param name="context">The source production context for reporting diagnostics.</param>
    /// <returns>The available capabilities, or null if validation failed.</returns>
    public static TableContextCapabilities? ValidateAndGetCapabilities(
        Compilation compilation,
        SourceProductionContext context)
    {
        bool hasTables = false, hasBlobs = false;

        foreach (AssemblyIdentity referencedAssembly in compilation.ReferencedAssemblyNames)
        {
            switch (referencedAssembly.Name)
            {
                case "TableStorage":
                    hasTables = true;
                    if (hasBlobs)
                    {
                        goto FoundBoth; // Early exit if we have both
                    }

                    break;

                case "TableStorage.Blobs":
                    hasBlobs = true;
                    if (hasTables)
                    {
                        goto FoundBoth; // Early exit if we have both
                    }

                    break;
            }
        }

    FoundBoth:
        if (!hasTables && !hasBlobs)
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
            return null;
        }

        return new TableContextCapabilities(hasTables, hasBlobs);
    }
}