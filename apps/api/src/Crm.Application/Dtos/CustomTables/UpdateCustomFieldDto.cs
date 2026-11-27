using Crm.Domain.Interfaces.Entities;

namespace Crm.Application.Dtos.CustomTables;

/// <summary>
/// Data transfer object for creating or updating a custom table field within a schema synchronization request.
/// Existing fields are matched by <paramref name="Id"/> while preserving their server-generated immutable key;
/// new fields omit <paramref name="Id"/> and receive a newly generated key.
/// </summary>
/// <param name="Id">The unique identifier of an existing field to update, or <see langword="null"/> for a new field.</param>
/// <param name="Name">The human-readable display name of the field.</param>
/// <param name="IsRequired">Whether a non-null string value is required for this field.</param>
/// <param name="Order">The zero-based display order of the field.</param>
public sealed record UpdateCustomFieldDto(
    Guid? Id,
    string Name,
    bool IsRequired,
    byte Order) : IIdentifiableField;
