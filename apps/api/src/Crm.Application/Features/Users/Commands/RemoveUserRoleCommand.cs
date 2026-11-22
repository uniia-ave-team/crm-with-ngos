using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Represents a command to remove a specific role from a user.
/// </summary>
/// <param name="UserId">The unique identifier of the user.</param>
/// <param name="RoleId">The unique identifier of the role to remove.</param>
public record RemoveUserRoleCommand(
    Guid UserId,
    Guid RoleId) : ICommand, ITransactionalCommand;
