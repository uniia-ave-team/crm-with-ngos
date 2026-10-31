using System.Collections.Frozen;
using System.Reflection;
using Crm.Domain.Entities;

namespace Crm.Domain.Extensions;

/// <summary>
/// Provides reflection-based helper extension methods and sets for the <see cref="CustomRecord"/> entity.
/// Facilitates the validation of reserved system keys and the identification of scalar sortable properties.
/// </summary>
public static class CustomRecordExtensions
{
    public static readonly FrozenSet<string> ReservedKeys;
    public static readonly FrozenDictionary<string, string> SortablePropertiesMap;

    static CustomRecordExtensions()
    {
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sortable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in typeof(CustomRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            reserved.Add(prop.Name);
            if (prop.Name != nameof(CustomRecord.Data) && prop.PropertyType.IsScalarType())
            {
                sortable[prop.Name] = prop.Name;
            }
        }

        ReservedKeys = reserved.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        SortablePropertiesMap = sortable.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the specified <see cref="Type"/> represents a scalar value type
    /// (e.g., primitive, enum, string, decimal, Guid, or date/time structure) that can be directly sorted in SQL via EF Core.
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
