namespace TableStorage;

internal sealed class BlobStorageFactory(string connectionString, bool autoCreate)
{
    private readonly BlobServiceClient _client = new(connectionString ?? throw new ArgumentNullException(nameof(connectionString)));
    private readonly bool _autoCreate = autoCreate;

    public async Task<BlobContainerClient> GetClient(string container)
    {
        BlobContainerClient client = _client.GetBlobContainerClient(container ?? throw new ArgumentNullException(nameof(container)));

        if (_autoCreate)
        {
            _ = await client.CreateIfNotExistsAsync();
        }

        return client;
    }
}