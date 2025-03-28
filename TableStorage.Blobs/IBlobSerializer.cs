
namespace TableStorage;

public interface IBlobSerializer
{
    public abstract BinaryData Serialize<T>(string table, T entity) where T : IBlobEntity;
    public abstract ValueTask<T?> DeserializeAsync<T>(string table, Stream entity, CancellationToken cancellationToken) where T : IBlobEntity;
}