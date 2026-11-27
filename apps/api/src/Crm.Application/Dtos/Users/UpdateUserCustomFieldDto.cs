using Crm.Domain.Interfaces.Entities;

namespace Crm.Application.Dtos.Users;

/// <summary>
/// Data transfer object for synchronizing a global user custom field definition.
/// Existing fields are matched by <paramref name="Id"/> while preserving their server-generated immutable key;
/// new fields omit <paramref name="Id"/> and receive a newly generated key.
/// </summary>
/// <param name="Id">The unique identifier of an existing field to update, or <see langword="null"/> for a new field.</param>
/// <param name="Name">The human-readable display name of the field.</param>
/// <param name="IsRequired">Whether a non-null string value is required for this field.</param>
/// <param name="IsPublic">Whether the field is visible to all authenticated users or restricted to authorized viewers.</param>
/// <param name="Order">The zero-based display order of the field.</param>
public sealed record UpdateUserCustomFieldDto(
    Guid? Id,
    string Name,
    bool IsRequired,
    bool IsPublic,
    byte Order) : IIdentifiableField;
