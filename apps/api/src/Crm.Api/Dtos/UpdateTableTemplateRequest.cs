using Crm.Application.Dtos.CustomTemplates;

namespace Crm.Api.Dtos;

/// <summary>
/// Represents the HTTP request payload to update an existing table template and its field structure.
/// </summary>
/// <param name="Name">The updated display name of the template.</param>
/// <param name="Description">The updated description.</param>
/// <param name="Fields">The full updated list of field schema definitions.</param>
public record UpdateTableTemplateRequest(
    string Name,
    string? Description,
    List<UpdateTemplateFieldDto> Fields);
