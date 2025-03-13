
namespace TableStorage;

public interface IBlobSerializer
{
    public abstract BinaryData Serialize<T>(T entity) where T : IBlobEntity;
    public abstract ValueTask<T?> DeserializeAsync<T>(Stream entity, CancellationToken cancellationToken) where T : IBlobEntity;
}