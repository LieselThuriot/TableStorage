using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;

namespace TableStorage.Fluent;

/// <summary>
/// Gives the <c>$type</c> discriminator a declared member, as C# doesn't allow that name. The Azure SDK uses the <see cref="DataMemberAttribute"/> name when translating filters.
/// </summary>
internal sealed class DollarType
{
    public const string Name = "$type";

    [DataMember(Name = Name)]
    public string? Type { get; set; }
}

internal static class DollarTypeClass
{
    private static readonly PropertyInfo s_property = typeof(DollarType).GetProperty(nameof(DollarType.Type))!;

    public static MemberExpression TypeProperty { get; } = Expression.Property(Expression.Parameter(typeof(DollarType), "x"), s_property);
}
