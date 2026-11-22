using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Represents a command to activate a previously deactivated user.
/// This restores the domain profile to an active state and unlocks the identity account.
/// </summary>
/// <param name="UserId">The unique identifier of the user to activate.</param>
/// <param name="CurrentUserId">The unique identifier of the user performing the action.</param>
public record ActivateUserCommand(Guid UserId, Guid CurrentUserId) : ICommand, ITransactionalCommand;
