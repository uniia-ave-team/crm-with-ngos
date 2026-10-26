using Crm.Domain.Enums;
using Crm.Domain.Interfaces;

namespace Crm.Application.Dtos.CustomTables;

/// <summary>
/// Represents a DTO for updating a field definition inside an existing custom table.
/// </summary>
/// <param name="Id">The unique identifier of the custom field, or <c>null</c> if appending a new field during table schema update.</param>
/// <param name="Name">The updated display name of the custom field.</param>
/// <param name="Key">The unique dictionary key used for internal JSON data serialization and mapping.</param>
/// <param name="DataType">The designated data type specifying validation rules and UI rendering.</param>
/// <param name="IsRequired">Indicates whether filling this field is mandatory when creating or updating records.</param>
/// <param name="Order">The display order sequence position of the field within the custom table schema.</param>
public record UpdateCustomFieldDto(
    Guid? Id,
    string Name,
    string Key,
    FieldDataType DataType,
    bool IsRequired,
    byte Order) : IIdentifiableField;
