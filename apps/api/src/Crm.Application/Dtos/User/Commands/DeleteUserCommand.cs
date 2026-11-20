using Crm.Application.Interfaces;
using Mediator;

namespace Crm.Application.Dtos.User.Commands;

/// <summary>
/// Command to permanently delete a user and all their personal data (GDPR hard-delete).
/// Acts as the DTO for the deletion request.
/// </summary>
/// <param name="UserId">The unique identifier of the user to delete.</param>
/// <param name="CurrentUserId">
/// The unique identifier of the authenticated user performing the deletion. When an administrator deletes another user,
/// projects solely managed by the deleted user are transferred to this administrator; self-deletion blocks while sole manager.
/// </param>
public record DeleteUserCommand(
    Guid UserId,
    Guid? CurrentUserId = null) : IRequest, ITransactionalCommand;
