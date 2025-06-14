using System.Text;
using TableStorage.SourceGenerators.Models;

namespace TableStorage.SourceGenerators.Generators.TableSetGeneration.CodeGeneration;

/// <summary>
/// Generates factory methods and blob support functionality.
/// </summary>
internal static class FactoryGenerator
{
    /// <summary>
    /// Generates the CreateTableSet factory method.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="classToGenerate">The class configuration.</param>
    /// <param name="context">The model context.</param>
    public static void GenerateTableSetFactoryMethod(StringBuilder sb, ClassToGenerate classToGenerate, in ModelContext context)
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

    /// <summary>
    /// Generates blob support members including CreateBlobSet and CreateAppendBlobSet methods.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="classToGenerate">The class configuration.</param>
    /// <param name="context">The model context.</param>
    public static void GenerateBlobSupportMembers(StringBuilder sb, ClassToGenerate classToGenerate, in ModelContext context)
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
        GenerateBlobSetFactory(sb, classToGenerate, context, "BlobSet", "CreateSet");
        
        // Generate CreateAppendBlobSet method
        GenerateBlobSetFactory(sb, classToGenerate, context, "AppendBlobSet", "CreateAppendSet");
    }

    private static void GenerateBlobSetFactory(StringBuilder sb, ClassToGenerate classToGenerate, in ModelContext context, string setType, string methodName)
    {
        sb.Append(@"

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static ").Append(setType).Append('<').Append(classToGenerate.Name).Append(@"> Create").Append(setType).Append(@"(TableStorage.IBlobCreator creator, string name)
        {
            return creator.").Append(methodName).Append('<').Append(classToGenerate.Name).Append(@">(name, ");

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
        }");
    }
}
