using System.Collections.Frozen;
using System.Reflection;
using Crm.Domain.Entities;

namespace Crm.Domain.Extensions;

/// <summary>
/// Provides reflection-based metadata identifying scalar sortable properties on <see cref="CustomRecord"/>.
/// </summary>
public static class CustomRecordExtensions
{
    /// <summary>
    /// Case-insensitive map of scalar <see cref="CustomRecord"/> property names that support direct SQL sorting.
    /// </summary>
    public static readonly FrozenDictionary<string, string> SortablePropertiesMap;

    static CustomRecordExtensions()
    {
        var sortable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in typeof(CustomRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.Name != nameof(CustomRecord.Data) && prop.PropertyType.IsScalarType())
            {
                sortable[prop.Name] = prop.Name;
            }
        }

        SortablePropertiesMap = sortable.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the specified <see cref="Type"/> represents a scalar value type
    /// that can be directly sorted in SQL via EF Core.
    /// </summary>
    /// <param name="type">The type to evaluate.</param>
    /// <returns><see langword="true"/> if the type is a scalar value; otherwise, <see langword="false"/>.</returns>
    public static bool IsScalarType(this Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return Type.GetTypeCode(underlying) != TypeCode.Object
            || underlying == typeof(Guid)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(DateOnly)
            || underlying == typeof(TimeOnly);
    }
}
