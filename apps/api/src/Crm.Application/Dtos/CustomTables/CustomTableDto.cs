namespace Crm.Application.Dtos.CustomTables;

/// <summary>
/// Represents the full schema DTO of a custom table, including all defined columns.
/// </summary>
/// <param name="Id">The unique identifier of the custom table.</param>
/// <param name="NgoId">The unique identifier of the owning organization.</param>
/// <param name="Name">The display name of the table.</param>
/// <param name="Description">An optional description explaining the table's purpose.</param>
/// <param name="SourceTemplateId">The optional identifier of the source template this table was instantiated from.</param>
/// <param name="Fields">The collection of field definitions making up the table schema.</param>
public record CustomTableDto(
    Guid Id,
    Guid NgoId,
    string Name,
    string? Description,
    Guid? SourceTemplateId,
    List<CustomFieldDto> Fields);
