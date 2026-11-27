using Crm.Domain.Enums;

namespace Crm.Application.Dtos.Users;

/// <summary>
/// Represents a global custom field column definition for user profiles.
/// Its <see cref="Id"/> is used directly as the property key inside user <c>customFields</c> JSON payloads and sorting parameters.
/// </summary>
/// <param name="Id">The unique identifier of the user custom field definition (also used as its JSON payload key).</param>
/// <param name="Name">The human-readable display name shown in table headers and forms.</param>
/// <param name="IsRequired">Indicates whether a non-null value is mandatory for every user.</param>
/// <param name="IsPublic">Indicates whether the field is visible to other users without <see cref="AccessRight.ViewCustomFields"/>.</param>
/// <param name="Order">The 0-based display order position of the column.</param>
public record UserCustomFieldDto(
    Guid Id,
    string Name,
    bool IsRequired,
    bool IsPublic,
    byte Order);
