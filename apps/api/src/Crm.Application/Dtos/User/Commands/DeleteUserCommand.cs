using Crm.Application.Interfaces;
using Mediator;

namespace Crm.Application.Dtos.User.Commands;

/// <summary>
/// Command to permanently delete a user and all their personal data (GDPR hard-delete).
/// Acts as the DTO for the deletion request.
/// </summary>
public record DeleteUserCommand(
    Guid UserId) : IRequest, ITransactionalCommand;
