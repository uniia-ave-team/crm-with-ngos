using Crm.Application.Dtos.CustomTemplates;
using Crm.Application.Dtos.CustomTemplates.Commands;

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
    List<UpdateTemplateFieldDto> Fields)
{
    /// <summary>
    /// Maps the request payload to an <see cref="UpdateTableTemplateCommand"/> instance.
    /// </summary>
    /// <param name="id">The unique identifier of the table template from the route.</param>
    public UpdateTableTemplateCommand ToCommand(Guid id) =>
        new(id, Name, Description, Fields);
}
