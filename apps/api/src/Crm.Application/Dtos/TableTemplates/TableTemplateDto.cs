using Crm.Application.Dtos.Common;

namespace Crm.Application.Dtos.TableTemplates;

/// <summary>
/// Data transfer object representing a reusable table template blueprint and its ordered field definitions.
/// </summary>
/// <param name="Id">The unique identifier of the table template.</param>
/// <param name="Name">The display name of the template.</param>
/// <param name="Description">An optional description of the template.</param>
/// <param name="IsSystemPreset">Whether the template is a read-only system preset.</param>
/// <param name="Fields">The ordered field blueprint definitions included in this template.</param>
public sealed record TableTemplateDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemPreset,
    List<FieldDefinitionDto> Fields);
