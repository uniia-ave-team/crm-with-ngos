using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.Roles.Commands;

/// <summary>
/// Represents a command to delete an existing role.
/// </summary>
/// <param name="Id">The unique identifier of the role to delete.</param>
public record DeleteRoleCommand(Guid Id) : ICommand, ITransactionalCommand;
