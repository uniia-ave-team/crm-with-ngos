using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.User.Commands;

/// <summary>
/// Represents a command to remove a specific custom key-value field from a user's profile.
/// </summary>
/// <param name="CustomFieldId">The unique identifier of the custom field entry to remove.</param>
/// <param name="UserId">The unique identifier of the user initiating the removal (used for ownership validation).</param>
public record RemoveUserCustomFieldCommand(
    Guid CustomFieldId,
    Guid UserId) : IRequest, ITransactionalCommand;
