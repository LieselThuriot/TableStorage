using System.Text.Json.Serialization;
using TableStorage.Tests.Models;

namespace TableStorage.Tests.Contexts;

[TableContext]
public partial class MyTableContext
{
    public TableSet<Model> Models1 { get; set; }
    public TableSet<Model2> Models2 { get; private set; }
    public TableSet<Model> Models3 { get; }
    public TableSet<Model> Models4 { get; init; }
    public TableSet<Model3> Models5 { get; init; }

    public BlobSet<Model> Models1Blob { get; set; }
    public BlobSet<Model4> Models4Blob { get; set; }
    public BlobSet<Model2> Models2Blob { get; }
    public AppendBlobSet<Model5> Models5Blob { get; }
    public AppendBlobSet<Model5> Models5BlobInJson { get; }

    // Base class property inheritance tests
    public TableSet<ModelBothKeysInBase> ModelBothKeysInBase { get; set; }
    public TableSet<ModelPartitionKeyInBase> ModelPartitionKeyInBase { get; set; }
    public TableSet<ModelRowKeyInBase> ModelRowKeyInBase { get; set; }
    public TableSet<ModelNoKeysInBase> ModelNoKeysInBase { get; set; }
    public TableSet<ModelBothKeysInBaseWithPartial> ModelBothKeysInBaseWithPartial { get; set; }
    public TableSet<ModelBothKeysInBaseWithOverride> ModelBothKeysInBaseWithOverride { get; set; }
}

[JsonSourceGenerationOptions(System.Text.Json.JsonSerializerDefaults.Web,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Model))]
[JsonSerializable(typeof(Model2))]
[JsonSerializable(typeof(Model4))]
[JsonSerializable(typeof(Model5))]
public partial class ModelSerializationContext : JsonSerializerContext;
