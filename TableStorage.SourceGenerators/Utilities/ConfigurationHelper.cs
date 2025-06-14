using Microsoft.CodeAnalysis.Diagnostics;

namespace TableStorage.SourceGenerators.Utilities;

/// <summary>
/// Utility class for extracting configuration values from analyzer options.
/// </summary>
internal static class ConfigurationHelper
{
    /// <summary>
    /// Extracts the PublishAot property value from analyzer configuration.
    /// </summary>
    /// <param name="optionsProvider">The analyzer configuration options provider.</param>
    /// <returns>True if PublishAot is enabled, false otherwise.</returns>
    public static bool GetPublishAotProperty(AnalyzerConfigOptionsProvider optionsProvider)
    {
        return optionsProvider.GlobalOptions.TryGetValue("build_property.PublishAot", out string? publishAotValue) &&
               bool.TryParse(publishAotValue, out bool parsedPublishAot) &&
               parsedPublishAot;
    }

    /// <summary>
    /// Extracts the TableStorageSerializerContext property value from analyzer configuration.
    /// </summary>
    /// <param name="optionsProvider">The analyzer configuration options provider.</param>
    /// <returns>The serializer context value, or null if not configured.</returns>
    public static string? GetTableStorageSerializerContextProperty(AnalyzerConfigOptionsProvider optionsProvider)
    {
        return optionsProvider.GlobalOptions.TryGetValue("build_property.TableStorageSerializerContext", out string? serializerContextValue)
            ? serializerContextValue
            : null;
    }
}
