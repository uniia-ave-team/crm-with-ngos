using Crm.Domain.Enums;
using Crm.Domain.Interfaces;

namespace Crm.Application.Dtos.CustomTables;

/// <summary>
/// Represents a DTO for creating a new custom field definition in a custom table.
/// </summary>
/// <param name="Name">The human-readable display name of the field seen by users.</param>
/// <param name="Key">The exact dictionary key used to map values inside the record JSON payload.</param>
/// <param name="DataType">The designated data type dictating validation rules and UI representation.</param>
/// <param name="IsRequired">Indicates whether filling this field is mandatory when saving a record.</param>
/// <param name="Order">The display order of the field in the table, starting from 0 for the first field.</param>
public record CreateCustomFieldDto(
    string Name,
    string Key,
    FieldDataType DataType,
    bool IsRequired,
    byte Order) : IFieldSchema;
