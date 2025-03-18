
namespace TableStorage;

public sealed class BlobOptions
{
    internal BlobOptions() { }

    public bool CreateContainerIfNotExists { get; set; } = true;

    public IBlobSerializer Serializer { get; set; } = default!;

    public bool UseTags { get; set; } = true;
}
