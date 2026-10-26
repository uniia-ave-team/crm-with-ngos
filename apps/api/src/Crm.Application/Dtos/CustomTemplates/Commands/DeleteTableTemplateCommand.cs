using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.CustomTemplates.Commands;

/// <summary>
/// Represents a command to delete a table template from the system.
/// </summary>
/// <param name="Id">The unique identifier of the template to delete.</param>
/// <returns>Unit signaling command completion.</returns>
public record DeleteTableTemplateCommand(
    Guid Id) : IRequest, ITransactionalCommand;
