using System.ComponentModel;
using TableStorage.Fluent;

namespace TableStorage.Linq;

/// <summary>
/// Entry point used by the generated fluent query helpers. Not intended to be used directly.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class FluentQuery
{
    /// <summary>
    /// Filters the table set to entities of type <typeparamref name="T"/>, which must be one of the generic arguments of <typeparamref name="TFluent"/>.
    /// </summary>
    public static IFilteredTableQueryable<T> WhereTypeIs<TFluent, T>(TableSet<TFluent> table)
        where TFluent : FluentEntityBase, new()
        where T : class, IDictionary<string, object>, ITableEntity, new()
    {
        if (!TypeCheck<TFluent, T>.IsValid)
        {
            throw new ArgumentException($"{typeof(T).Name} is not one of the types of {typeof(TFluent).Name}.", nameof(T));
        }

#if NET
        ArgumentNullException.ThrowIfNull(table);
#else
        if (table is null)
        {
            throw new ArgumentNullException(nameof(table));
        }
#endif

        return new FluentSetQueryHelper<T, TFluent>(TableSetQueryHelper.From(table));
    }

    private static class TypeCheck<TFluent, T>
    {
        public static readonly bool IsValid = typeof(TFluent).IsGenericType && Array.IndexOf(typeof(TFluent).GetGenericArguments(), typeof(T)) >= 0;
    }
}
