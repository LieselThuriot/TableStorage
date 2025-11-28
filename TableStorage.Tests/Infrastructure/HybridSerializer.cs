#nullable disable

using ProtoBuf;
using System.Text.Json;
using TableStorage.Tests.Contexts;
using TableStorage.Tests.Models;

namespace TableStorage.Tests.Infrastructure;

public sealed class HybridSerializer : IBlobSerializer
{
    public async ValueTask<T> DeserializeAsync<T>(string table, Stream entity, CancellationToken cancellationToken) where T : IBlobEntity
    {
        if (table is "models4blob")
        {
            return Serializer.Deserialize<T>(entity);
        }

        if (table is "models5blob")
        {
            using StreamReader reader = new(entity);
            string simple = await reader.ReadToEndAsync(cancellationToken);
            string[] parts = simple.Split('\\', 3);

            return (T)(object)new Model5
            {
                Id = parts[0],
                ContinuationToken = parts[1],
                Entries = [.. parts[2].Split('|').Select(x =>
                {
                    string[] entryParts = x.Split(';');
                    return new Model5Entry
                    {
                        Creation = DateTimeOffset.FromUnixTimeSeconds(long.Parse(entryParts[0])),
                        Duration = entryParts[1] switch
                        {
                            null or "" => null,
                            _ => long.Parse(entryParts[1])
                        }
                    };
                })]
            };
        }

        if (table is "models5blobinjson")
        {
            return (T)(object)await JsonSerializer.DeserializeAsync(entity, ModelSerializationContext.Default.Model5, cancellationToken)!;
        }

        BinaryData data = await BinaryData.FromStreamAsync(entity, cancellationToken);
        return data.ToObjectFromJson<T>(ModelSerializationContext.Default.Options)!;
    }

    public BinaryData Serialize<T>(string table, T entity) where T : IBlobEntity
    {
        if (table is "models4blob" && entity is Model4 model4)
        {
            using MemoryStream stream = new();
            Serializer.Serialize(stream, model4);
            return new(stream.ToArray());
        }

        if (table is "models5blob" && entity is Model5 model5)
        {
            string simple = $"{model5.Id}\\{model5.ContinuationToken}\\{string.Join("|", model5.Entries.Select(x => $"{x.Creation.ToUnixTimeSeconds()};{x.Duration}"))}";
            return BinaryData.FromString(simple);
        }

        if (table is "models5blobinjson" && entity is Model5 model5InJson)
        {
            byte[] data = JsonSerializer.SerializeToUtf8Bytes(model5InJson, ModelSerializationContext.Default.Model5);
            return BinaryData.FromBytes(data);
        }

        return BinaryData.FromObjectAsJson(entity, ModelSerializationContext.Default.Options);
    }
}