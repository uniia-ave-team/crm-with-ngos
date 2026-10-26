namespace Crm.Application.Dtos.CustomTemplates;

/// <summary>
/// Represents the full DTO of a reusable table template blueprint.
/// </summary>
/// <param name="Id">The unique identifier of the table template.</param>
/// <param name="Name">The display name of the template preset.</param>
/// <param name="Description">An optional explanation of intended use cases.</param>
/// <param name="IsSystemPreset">Indicates whether this template is a global default preset provided by the system.</param>
/// <param name="Fields">The collection of field blueprints making up this template schema.</param>
public record TableTemplateDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemPreset,
    List<TemplateFieldDto> Fields);
