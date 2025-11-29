using System.Collections;

namespace TableStorage;

#nullable disable

public class FluentTableEntity<TFirst, TSecond> : IDictionary<string, object>, ITableEntity
    where TFirst : class, IDictionary<string, object>, ITableEntity, new()
    where TSecond : class, IDictionary<string, object>, ITableEntity, new()
{
    private enum BackingType
    {
        None,
        First,
        Second
    }

    private BackingType _backingType;
    private IDictionary<string, object> _backingDictionary;

    /// <summary>
    /// This should only be used by the Table SDK when deserializing entities.
    /// </summary>
    public FluentTableEntity()
    {
        _backingType = BackingType.None;
        _backingDictionary = new Dictionary<string, object>();
    }

    public FluentTableEntity(TFirst entity)
    {
        _backingType = BackingType.First;
        _backingDictionary = entity;
    }

    public static implicit operator FluentTableEntity<TFirst, TSecond>(TFirst entity) => new(entity);
    public static implicit operator TFirst(FluentTableEntity<TFirst, TSecond> entity) => (TFirst)entity._backingDictionary;

    public FluentTableEntity(TSecond entity)
    {
        _backingType = BackingType.Second;
        _backingDictionary = entity;
    }

    public static implicit operator FluentTableEntity<TFirst, TSecond>(TSecond entity) => new(entity);
    public static implicit operator TSecond(FluentTableEntity<TFirst, TSecond> entity) => (TSecond)entity._backingDictionary;

    public string PartitionKey
    {
        get => _backingDictionary["PartitionKey"].ToString();
        set => _backingDictionary["PartitionKey"] = value;
    }

    public string RowKey
    {
        get => _backingDictionary["RowKey"].ToString();
        set => _backingDictionary["RowKey"] = value;
    }
    public DateTimeOffset? Timestamp
    {
        get => _backingDictionary["Timestamp"] is DateTimeOffset dateTimeOffset ? dateTimeOffset : default;
        set => _backingDictionary["Timestamp"] = value;
    }
    public ETag ETag
    {
        get => _backingDictionary["ETag"] is ETag eTag ? eTag : default;
        set => _backingDictionary["ETag"] = value;
    }

    public object this[string key]
    {
        get
        {
            if (key == "$type")
            {
                return _backingType switch
                {
                    BackingType.First => typeof(TFirst).Name,
                    BackingType.Second => typeof(TSecond).Name,
                    _ => null
                };
            }

            return _backingDictionary[key];
        }

        set
        {
            if (key == "$type")
            {
                string type = value?.ToString();

                if (type == typeof(TFirst).Name)
                {
                    _backingType = BackingType.First;
                    Create<TFirst>();
                }
                else if (type == typeof(TSecond).Name)
                {
                    _backingType = BackingType.Second;
                    Create<TSecond>();
                }
                else
                {
                    throw new InvalidOperationException($"Invalid type specified: {value}");
                }

                void Create<T>() where T : class, IDictionary<string, object>, ITableEntity, new()
                {
                    var original = _backingDictionary;
                    _backingDictionary = new T();

                    foreach (var kvp in original)
                    {
                        _backingDictionary[kvp.Key] = kvp.Value;
                    }
                }
            }
            else
            {
                _backingDictionary[key] = value;
            }
        }
    }

    public ICollection<string> Keys => ["$type", .. _backingDictionary.Keys];
    public ICollection<object> Values => [this["$type"], .. _backingDictionary.Values];

    public int Count => _backingDictionary.Count + 1;
    public bool IsReadOnly => false;

    public void Add(string key, object value) => _backingDictionary.Add(key, value);
    public bool ContainsKey(string key) => _backingDictionary.ContainsKey(key);
    public bool Remove(string key) => _backingDictionary.Remove(key);
    public bool TryGetValue(string key, out object value) => _backingDictionary.TryGetValue(key, out value);
    public void Add(KeyValuePair<string, object> item) => _backingDictionary.Add(item);
    public void Clear() => _backingDictionary.Clear();
    public bool Contains(KeyValuePair<string, object> item) => _backingDictionary.Contains(item);
    public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) => _backingDictionary.CopyTo(array, arrayIndex);
    public bool Remove(KeyValuePair<string, object> item) => _backingDictionary.Remove(item);
    public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _backingDictionary.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_backingDictionary).GetEnumerator();
}
