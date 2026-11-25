using Crm.Domain.Enums;

namespace Crm.Domain.Common;

/// <summary>
/// Represents a resolved dynamic sort specification targeting a user custom field key and its SQL data type.
/// </summary>
/// <param name="Key">The canonical custom field key.</param>
/// <param name="DataType">The resolved data type used for strongly-typed SQL casting.</param>
public record UserCustomFieldSort(
    string Key,
    FieldDataType DataType);
