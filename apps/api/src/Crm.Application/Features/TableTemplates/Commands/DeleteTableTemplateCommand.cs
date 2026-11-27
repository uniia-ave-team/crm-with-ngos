using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.TableTemplates.Commands;

/// <summary>
/// Represents a command to delete a table template from the system.
/// </summary>
/// <param name="Id">The unique identifier of the template to delete.</param>
/// <returns>Unit signaling command completion.</returns>
public record DeleteTableTemplateCommand(
    Guid Id) : ICommand, ITransactionalCommand, IHasEntityId;
