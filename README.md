﻿# TableStorage
Streamlined way of working with Azure Data Tables and Blobs.

## Installation

```bash
dotnet add package TableStorage
dotnet add package TableStorage.Blobs
```

## Usage

Create your own TableContext and mark it with the `[TableContext]` attribute. This class must be partial.

```csharp
[TableContext]
public partial class MyTableContext;
```

Create your models, these must be classes and have a parameterless constructor. Mark them with the `[TableSet]` attribute. This class must be partial.

```csharp
[TableSet]
public partial class Model
{
    public string Data { get; set; }
    public bool Enabled { get; set; }
}
```

Properties can also be defined using the `[TableSetProperty]` attribute. 
This is particularly useful if you are planning on using dotnet 8+'s Native AOT, as the source generation will make sure any breaking reflection calls are avoided by the Azure.Core libraries.
Starting C# 13, you can also mark them as partial.

```csharp
[TableSet]
[TableSetProperty(typeof(string), "Data")]
[TableSetProperty(typeof(bool), "Enabled")]
public partial class Model;
```

Some times it's also nice to have a pretty name for your `PartitionKey` and `RowKey` properties, as the original names might not always make much sense when reading your code, at least not in a functional way.
You can use the `PartitionKey` and `RowKey` properties of `TableSet` to create a proxy for these two properties.

```csharp
[TableSet(PartitionKey = "MyPrettyPartitionKey", RowKey = "MyPrettyRowKey")]
public partial class Model;
```

`TableSet` also has `TrackChanges` property, default `false`, that will try to optimize what is being sent back to the server when making changes to an entity.
When tracking changes, it's important to either use the `TableSetProperty` attribute to define your properties, or mark them as partial starting C# 13, otherwise they will not be tracked.

```csharp
[TableSet]
[TableSetProperty(typeof(string), "Data")]
public partial class Model
{
    public partial bool Enabled { get; set; }
}
```

Besides tracking changes, you can also mark the model for Blob storage support. You can do this by setting `SupportBlobs` on the `TableSet` attribute to `true`.
When working with blobs, you can mark certain properties to be used as blob tags, either by decorating the property with `[Tag]` or by setting `Tag` to `true` on the `TableSetProperty` attribute.

```csharp
[TableSet(SupportBlobs = true)]
[TableSetProperty(typeof(string), "Data", Tag = true)]
public partial class Model
{
    [Tag]
    public partial bool Enabled { get; set; }
}
```

Important: If you plan on using the default STJ serialization, or plan on using the source generated `JsonSerializerContext`, you need to make sure that the properties you want to serialize are defined on your partial class definition. This includes your partition and rowkey. If you do not do this, STJ will not serialize them.

Place your tables on your TableContext. The sample below will create 2 tables in table storage, named Models1 and Models2. It will also create a blob container named BlobModels1 which is a set for Block blobs. BlobModels2 is a set for Append blobs.

```csharp
[TableContext]
public partial class MyTableContext
{
    public TableSet<Model> Models1 { get; set; }
    public BlobSet<Model> BlobModels1 { get; set; }
    public AppendBlobSet<Model> BlobModels2 { get; set; }
    public TableSet<Model> Models2 { get; set; }
}
```

Register your TableContext in your services. An extension method will be available specifically for your context.

```csharp
builder.Services.AddMyTableContext(builder.Configuration.GetConnectionString("MyConnectionString"));
```

Optionally, pass along a `Configure` method to adjust some configuration options.

```csharp
builder.Services.AddMyTableContext(builder.Configuration.GetConnectionString("MyConnectionString"), Configure);

static void Configure(TableOptions options)
{
    options.TableMode = TableUpdateMode.Merge;
}
```

If you have defined any `BlobSets`, a third parameter becomes available to configure the blob service.

```csharp
builder.Services.AddMyTableContext(builder.Configuration.GetConnectionString("MyConnectionString"), ConfigureTables, ConfigureBlobs);

static void ConfigureTables(TableOptions options)
{
    options.TableMode = TableUpdateMode.Merge;
}

static void ConfigureBlobs(BlobOptions options)
{
    options.UseTags = true;
}
```

Inject `MyTableContext` into your class and use as needed.

```csharp
public class MyService(MyTableContext context)
{
    private readonly MyTableContext _context = context;

    public async Task DoSomething(CancellationToken token)
    {
        var entity = await _context.Models1.GetEntityOrDefaultAsync("partitionKey", "rowKey", token);
        if (entity is not null)
        {
            //Do more
        }
    }
}
```

For some special cases, your table name might not be known at compile time. To handle those, an extension method has been added:

```csharp
var tableSet = context.GetTableSet<Model>("randomname");
```

## Linq

A few simple Linq extension methods have been provided in the `TableStorage.Linq` namespace that optimize some existing LINQ methods specifically for Table Storage.

Since these return an instance that implements `IAsyncEnumerable`, `System.Linq.Async` is an excellent companion to these methods. Do keep in mind that as soon as you start using `IAsyncEnumerable`, any further operations will run client-side.


Note: `Select` will include the actual transformation. If you want the original model, with only the selected fields retrieved, use `SelectFields` instead.
If you are using Native AOT, you will need to use `SelectFields` as `Select` will not work.


## Custom Serialization

Blob storage allows for custom serialization and deserialization. By default, `System.Text.Json` will be used for serialization. 
You can define your own by implementing `IBlobSerializer` and passing it to the `BlobOptions` object.

Here's an example for a model that uses ProtoBuf:
```csharp
builder.Services.AddMyTableContext(builder.Configuration.GetConnectionString("MyConnectionString"), ConfigureTables, ConfigureBlobs);

static void ConfigureTables(TableOptions options)
{
    options.TableMode = TableUpdateMode.Merge;
}

static void ConfigureBlobs(BlobOptions options)
{
    options.UseTags = true;
    options.Serializer = new ProtoBufSerializer();
}

[TableSet(PartitionKey = nameof(PrettyPartition), RowKey = nameof(PrettyRow), SupportBlobs = true)]
[ProtoContract(IgnoreListHandling = true)] // Important to ignore list handling because we are generating an IDictionary implementation that is not supported by protobuf
public partial class Model
{
    [ProtoMember(1)] public partial string PrettyPartition { get; set; } // We can partial the PK and RowKey to enable custom serialization attributes
    [ProtoMember(2)] public partial string PrettyRow { get; set; }
    [ProtoMember(3)] public partial int MyProperty1 { get; set; }
    [ProtoMember(4)] public partial string MyProperty2 { get; set; }
    [ProtoMember(5)] public partial string? MyNullableProperty2 { get; set; }
}

public sealed class ProtoBufSerializer : IBlobSerializer
{
    public async ValueTask<T> DeserializeAsync<T>(Stream entity, CancellationToken cancellationToken) where T : IBlobEntity
    {
        return Serializer.Deserialize<T>(entity);
    }

    public BinaryData Serialize<T>(T entity) where T : IBlobEntity
    {
        using MemoryStream stream = new();
        Serializer.Serialize(stream, model4);
        return new(stream.ToArray());
    }
}
```

For some specific cases, the source generator will have to generate a `.Deserialize` call using System.Text.Json.
Since this is not supported when publishing with Native AOT, you can use the `TableStorageSerializerContext` property in your csproj file to set the fullname of a class that implements `JsonSerializerContext` to support native deserialization.

```xml
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<OutputType>Exe</OutputType>
		<TargetFramework>net9.0</TargetFramework>
		<PublishAot>true</PublishAot>
		<TableStorageSerializerContext>TableStorage.Tests.Contexts.ModelSerializationContext</TableStorageSerializerContext>
	</PropertyGroup>
</Project>
```

When configuring your context, you can also pass a `JsonSerializerContext` to the `BlobOptions` object to support native deserialization. Otherwise the default serialization will be used that relies on reflection.

```csharp
static void ConfigureBlobs(BlobOptions options)
{
    options.Serializer = new AotJsonBlobSerializer(MyJsonSerializerContext.Default);
}
```