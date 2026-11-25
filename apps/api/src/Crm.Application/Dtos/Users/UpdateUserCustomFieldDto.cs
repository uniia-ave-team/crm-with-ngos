using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Entities;

namespace Crm.Application.Dtos.Users;

/// <summary>
/// Represents a DTO for creating, updating, or reordering a global user custom field definition.
/// </summary>
/// <param name="Id">The unique identifier of an existing custom field, or <see langword="null"/> when creating a new field.</param>
/// <param name="Name">The human-readable display name of the custom field.</param>
/// <param name="Key">The unique snake_case dictionary key used in user <c>customFields</c> JSON payloads.</param>
/// <param name="DataType">The designated data type specifying validation rules and UI rendering.</param>
/// <param name="IsRequired">Indicates whether filling this field is mandatory for all users.</param>
/// <param name="IsPublic">Indicates whether the field is visible to other regular users.</param>
/// <param name="Order">The 0-based display order position of the field.</param>
public record UpdateUserCustomFieldDto(
    Guid? Id,
    string Name,
    string Key,
    FieldDataType DataType,
    bool IsRequired,
    bool IsPublic,
    byte Order) : IIdentifiableField;
