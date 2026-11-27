namespace Crm.Application.Dtos.CustomTables;

/// <summary>
/// Represents a detailed DTO for an existing custom field definition within a table schema.
/// Its <see cref="Id"/> is used directly as the property key inside custom record JSON payloads and sorting parameters.
/// </summary>
/// <param name="Id">The unique identifier of the custom field (also used as its JSON payload key).</param>
/// <param name="Name">The human-readable display name of the field.</param>
/// <param name="IsRequired">Indicates whether filling this field is mandatory.</param>
/// <param name="Order">The display order of the field in the table, starting from 0 for the first field.</param>
public record CustomFieldDto(
    Guid Id,
    string Name,
    bool IsRequired,
    byte Order);
