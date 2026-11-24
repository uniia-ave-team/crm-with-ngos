using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Represents a command to assign a specific role to a user.
/// </summary>
/// <param name="UserId">The unique identifier of the user.</param>
/// <param name="RoleId">The unique identifier of the role to assign.</param>
public record AssignUserRoleCommand(
    Guid UserId,
    Guid RoleId) : ICommand, ITransactionalCommand;
