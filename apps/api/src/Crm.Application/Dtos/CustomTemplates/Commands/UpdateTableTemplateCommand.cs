using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.CustomTemplates.Commands;

/// <summary>
/// Represents a command to update an existing table template and its field structure.
/// </summary>
/// <param name="Id">The unique identifier of the template to update.</param>
/// <param name="Name">The updated display name of the template.</param>
/// <param name="Description">The updated description.</param>
/// <param name="Fields">The full updated list of field schema definitions.</param>
/// <returns>Unit signaling command completion.</returns>
public record UpdateTableTemplateCommand(
    Guid Id,
    string Name,
    string? Description,
    List<UpdateTemplateFieldDto> Fields) : IRequest, ITransactionalCommand;
