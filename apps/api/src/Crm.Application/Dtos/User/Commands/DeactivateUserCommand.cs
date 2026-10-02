using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.User.Commands;
/// <summary>
/// Represents a command to deactivate a user in the system.
/// This performs a soft-delete on the domain profile and locks the identity account.
/// </summary>
/// <param name="UserId">The unique identifier of the user to deactivate.</param>
/// <param name="CurrentUserId">The unique identifier of the user performing the action.</param>
public record DeactivateUserCommand(Guid UserId, Guid CurrentUserId) : IRequest, ITransactionalCommand;
