using Crm.Domain.Enums;

namespace Crm.Application.Dtos.CustomTables;

/// <summary>
/// Represents a detailed DTO for an existing custom field definition within a table schema.
/// </summary>
/// <param name="Id">The unique identifier of the custom field.</param>
/// <param name="Name">The human-readable display name of the field.</param>
/// <param name="Key">The exact dictionary key used for JSON payload serialization.</param>
/// <param name="DataType">The data type specifying validation rules and UI rendering.</param>
/// <param name="IsRequired">Indicates whether filling this field is mandatory.</param>
/// <param name="Order">The display order of the field in the table, starting from 0 for the first field.</param>
public record CustomFieldDto(
    Guid Id,
    string Name,
    string Key,
    FieldDataType DataType,
    bool IsRequired,
    byte Order);
