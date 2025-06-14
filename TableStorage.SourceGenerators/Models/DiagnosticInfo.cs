using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using TableStorage.SourceGenerators.Utilities;

namespace TableStorage.SourceGenerators.Models;

/// <summary>
/// Contains information needed to create a diagnostic, with proper structural equality for caching.
/// This follows best practices for diagnostics in incremental generators by avoiding direct
/// storage of Location objects which don't implement proper equality.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DiagnosticInfo"/> struct.
/// </remarks>
/// <param name="descriptor">The diagnostic descriptor.</param>
/// <param name="location">The location information.</param>
internal readonly struct DiagnosticInfo(DiagnosticDescriptor descriptor, LocationInfo? location) : IEquatable<DiagnosticInfo>
{
    public readonly DiagnosticDescriptor Descriptor = descriptor;
    public readonly LocationInfo? Location = location;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticInfo"/> struct with automatic location conversion.
    /// </summary>
    /// <param name="descriptor">The diagnostic descriptor.</param>
    /// <param name="location">The location where the diagnostic should be reported.</param>
    public DiagnosticInfo(DiagnosticDescriptor descriptor, Location? location)
        : this(descriptor, location is not null ? LocationInfo.CreateFrom(location) : null)
    {
    }

    /// <summary>
    /// Creates a Diagnostic instance from this DiagnosticInfo.
    /// </summary>
    /// <returns>A Diagnostic that can be reported to the compilation.</returns>
    public Diagnostic CreateDiagnostic()
    {
        Location location = Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None;
        return Diagnostic.Create(Descriptor, location);
    }

    public bool Equals(DiagnosticInfo other)
    {
        return Equals(Descriptor, other.Descriptor) && Nullable.Equals(Location, other.Location);
    }

    public override bool Equals(object? obj)
    {
        return obj is DiagnosticInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Descriptor, Location);
    }

    public static bool operator ==(DiagnosticInfo left, DiagnosticInfo right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(DiagnosticInfo left, DiagnosticInfo right)
    {
        return !left.Equals(right);
    }
}

/// <summary>
/// Contains location information with proper structural equality for caching.
/// This replaces the use of Location objects in the incremental generator pipeline.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="LocationInfo"/> struct.
/// </remarks>
/// <param name="filePath">The file path.</param>
/// <param name="textSpan">The text span.</param>
/// <param name="lineSpan">The line position span.</param>
internal readonly struct LocationInfo(string filePath, TextSpan textSpan, LinePositionSpan lineSpan) : IEquatable<LocationInfo>
{
    public readonly string FilePath = filePath;
    public readonly TextSpan TextSpan = textSpan;
    public readonly LinePositionSpan LineSpan = lineSpan;

    /// <summary>
    /// Creates a Location instance from this LocationInfo.
    /// </summary>
    /// <returns>A Location that can be used for diagnostic reporting.</returns>
    public Location ToLocation()
    {
        return Location.Create(FilePath, TextSpan, LineSpan);
    }

    /// <summary>
    /// Creates a LocationInfo from a SyntaxNode.
    /// </summary>
    /// <param name="node">The syntax node to extract location from.</param>
    /// <returns>A LocationInfo representing the node's location, or null if unavailable.</returns>
    public static LocationInfo? CreateFrom(SyntaxNode node)
    {
        return CreateFrom(node.GetLocation());
    }

    /// <summary>
    /// Creates a LocationInfo from a Location.
    /// </summary>
    /// <param name="location">The location to convert.</param>
    /// <returns>A LocationInfo representing the location, or null if the location has no source tree.</returns>
    public static LocationInfo? CreateFrom(Location location)
    {
        if (location.SourceTree is null)
        {
            return null;
        }

        return new LocationInfo(
            location.SourceTree.FilePath,
            location.SourceSpan,
            location.GetLineSpan().Span);
    }

    public bool Equals(LocationInfo other)
    {
        return FilePath == other.FilePath && TextSpan.Equals(other.TextSpan) && LineSpan.Equals(other.LineSpan);
    }

    public override bool Equals(object? obj)
    {
        return obj is LocationInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(FilePath, TextSpan, LineSpan);
    }

    public static bool operator ==(LocationInfo left, LocationInfo right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(LocationInfo left, LocationInfo right)
    {
        return !left.Equals(right);
    }
}

/// <summary>
/// A result type that can hold either a value or diagnostic information.
/// This follows functional programming patterns and ensures proper caching in incremental generators.
/// </summary>
/// <typeparam name="TValue">The type of the value when successful.</typeparam>
internal readonly struct Result<TValue>(TValue? value, EquatableArray<DiagnosticInfo> diagnostics) : IEquatable<Result<TValue>>
    where TValue : IEquatable<TValue>
{
    public readonly TValue? Value = value;
    public readonly EquatableArray<DiagnosticInfo> Diagnostics = diagnostics;

    /// <summary>
    /// Gets a value indicating whether this result represents a successful operation.
    /// </summary>
    public bool IsSuccess => Value is not null && Diagnostics.IsEmpty;

    /// <summary>
    /// Gets a value indicating whether this result has diagnostic information.
    /// </summary>
    public bool HasDiagnostics => !Diagnostics.IsEmpty;

    /// <summary>
    /// Creates a successful result with the specified value.
    /// </summary>
    /// <param name="value">The successful value.</param>
    /// <returns>A successful result.</returns>
    public static Result<TValue> Success(TValue value)
    {
        return new Result<TValue>(value, new EquatableArray<DiagnosticInfo>(null));
    }

    /// <summary>
    /// Creates a failed result with the specified diagnostics.
    /// </summary>
    /// <param name="diagnostics">The diagnostic information.</param>
    /// <returns>A failed result.</returns>
    public static Result<TValue> Failure(params DiagnosticInfo[] diagnostics)
    {
        return new Result<TValue>(default, new EquatableArray<DiagnosticInfo>(diagnostics));
    }

    /// <summary>
    /// Creates a result with both a value and diagnostics (warnings).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="diagnostics">The diagnostic information.</param>
    /// <returns>A result with both value and diagnostics.</returns>
    public static Result<TValue> SuccessWithDiagnostics(TValue value, params DiagnosticInfo[] diagnostics)
    {
        return new Result<TValue>(value, new EquatableArray<DiagnosticInfo>(diagnostics));
    }

    public bool Equals(Result<TValue> other)
    {
        return EqualityComparer<TValue?>.Default.Equals(Value, other.Value) && Diagnostics.Equals(other.Diagnostics);
    }

    public override bool Equals(object? obj)
    {
        return obj is Result<TValue> other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Value, Diagnostics);
    }

    public static bool operator ==(Result<TValue> left, Result<TValue> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Result<TValue> left, Result<TValue> right)
    {
        return !left.Equals(right);
    }
}
