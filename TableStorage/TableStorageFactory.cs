#if NET
using Azure.Data.Tables.Models;
using System.Text.Json;
#endif

namespace TableStorage;

internal sealed class TableStorageFactory(TableServiceClient client, CreateIfNotExistsMode mode)
{
    private readonly TableServiceClient _client = client;
    private readonly CreateIfNotExistsMode _creationMode = mode;

    private readonly HashSet<string> _createdTables = new(StringComparer.OrdinalIgnoreCase);

    public Task<TableClient> GetClient(string tableName)
    {
        TableClient client = _client.GetTableClient(tableName ?? throw new ArgumentNullException(nameof(tableName)));

        return _creationMode switch
        {
            CreateIfNotExistsMode.Always => Init(client),
            CreateIfNotExistsMode.Once when ShouldInit(tableName) => Init(client),
            _ => Task.FromResult(client),
        };
    }

    private bool ShouldInit(string tableName)
    {
        lock (_createdTables)
        {
            return _createdTables.Add(tableName);
        }
    }

    private static async Task<TableClient> Init(TableClient client)
    {
#if NET
        // CreateIfNotExists serializes its request with reflection-based JSON, which isn't available with Native AOT.
        if (!JsonSerializer.IsReflectionEnabledByDefault)
        {
            try
            {
                _ = await client.CreateAsync();
            }
            catch (RequestFailedException ex) when (ex.Status is 409 && ex.ErrorCode != TableErrorCode.TableBeingDeleted)
            {
                // Table already exists
            }

            return client;
        }
#endif

        _ = await client.CreateIfNotExistsAsync();
        return client;
    }
}
