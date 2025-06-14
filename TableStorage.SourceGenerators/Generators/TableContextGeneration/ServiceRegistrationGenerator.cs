using System.Text;

namespace TableStorage.SourceGenerators.Generators.TableContextGeneration;

/// <summary>
/// Generates service registration methods for dependency injection.
/// </summary>
internal static class ServiceRegistrationGenerator
{
    /// <summary>
    /// Generates the static Register method for dependency injection setup.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="className">The name of the context class.</param>
    /// <param name="hasTables">Whether table support is available.</param>
    /// <param name="hasBlobs">Whether blob support is available.</param>
    public static void GenerateRegistrationMethod(
        StringBuilder sb, 
        string className, 
        bool hasTables, 
        bool hasBlobs)
    {
        sb.Append(@"

        public static void Register(IServiceCollection services, string connectionString");

        // Add configuration parameters
        if (hasTables)
        {
            sb.Append(", Action<TableStorage.TableOptions> configure");
        }

        if (hasBlobs)
        {
            sb.Append(", Action<TableStorage.BlobOptions> configureBlobs");
        }

        sb.Append(@")
        {
            services.AddSingleton(s =>
            {");

        // Generate creator setup
        GenerateCreatorSetup(sb, hasTables, hasBlobs);

        // Generate return statement
        sb.Append(@"
                return new ").Append(className).Append('(');

        if (hasTables)
        {
            sb.Append("creator");
        }

        if (hasBlobs)
        {
            if (hasTables)
            {
                sb.Append(", ");
            }
            
            sb.Append("blobCreator");
        }

        sb.Append(@");
            });
        }");
    }

    private static void GenerateCreatorSetup(StringBuilder sb, bool hasTables, bool hasBlobs)
    {
        if (hasTables)
        {
            sb.Append(@"
                TableStorage.ICreator creator = TableStorage.TableStorageSetup.BuildCreator(connectionString, configure);");
        }

        if (hasBlobs)
        {
            sb.Append(@"
                TableStorage.IBlobCreator blobCreator = TableStorage.BlobStorageSetup.BuildCreator(connectionString, configureBlobs);");
        }
    }
}
