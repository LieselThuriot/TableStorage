using System.Collections;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace TableStorage.Fluent.SourceGenerators;

internal readonly record struct LocationInfo(string FilePath, TextSpan Span, LinePositionSpan LineSpan)
{
    public Location ToLocation() => Location.Create(FilePath, Span, LineSpan);

    public static LocationInfo? From(Location? location)
    {
        if (location?.SourceTree is null)
        {
            return null;
        }

        return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
    }
}

/// <summary>
/// A request to generate the fluent types for a given number of generic type parameters.
/// </summary>
internal readonly record struct ArityRequest(int Arity, bool IsPublic, bool FromAttribute, LocationInfo? Location);

/// <summary>
/// Information about the compilation that influences generation.
/// </summary>
internal readonly record struct CompilationInfo(bool HasFluentRuntime, EquatableArray<int> ExternallyProvided);

internal readonly struct EquatableArray<T>(ImmutableArray<T> array) : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _array = array;

    public ImmutableArray<T> AsImmutableArray() => _array.IsDefault ? [] : _array;

    public bool Equals(EquatableArray<T> other) => AsImmutableArray().SequenceEqual(other.AsImmutableArray());

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        int hash = 17;
        foreach (T item in AsImmutableArray())
        {
            hash = (hash * 31) + (item?.GetHashCode() ?? 0);
        }

        return hash;
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)AsImmutableArray()).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
