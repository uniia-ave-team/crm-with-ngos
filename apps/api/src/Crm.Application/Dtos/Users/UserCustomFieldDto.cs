using Crm.Domain.Enums;

namespace Crm.Application.Dtos.Users;

/// <summary>
/// Represents a global custom field column definition for user profiles.
/// </summary>
/// <param name="Id">The unique identifier of the user custom field definition.</param>
/// <param name="Name">The human-readable display name shown in table headers and forms.</param>
/// <param name="Key">The unique snake_case key used in user <c>customFields</c> JSON payloads.</param>
/// <param name="DataType">The data type specifying validation rules, typed sorting, and UI representation.</param>
/// <param name="IsRequired">Indicates whether a non-null value is mandatory for every user.</param>
/// <param name="IsPublic">Indicates whether the field is visible to other users without <see cref="AccessRight.ViewCustomFields"/>.</param>
/// <param name="Order">The 0-based display order position of the column.</param>
public record UserCustomFieldDto(
    Guid Id,
    string Name,
    string Key,
    FieldDataType DataType,
    bool IsRequired,
    bool IsPublic,
    byte Order);
