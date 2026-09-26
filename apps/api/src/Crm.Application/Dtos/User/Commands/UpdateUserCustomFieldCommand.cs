using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.User.Commands;

/// <summary>
/// Represents a command to update the value of a specific custom field in a user's profile.
/// </summary>
/// <param name="CustomFieldId">The unique identifier of the custom field entry to update.</param>
/// <param name="UserId">The unique identifier of the user initiating the update (used for ownership validation).</param>
/// <param name="Value">The updated string value.</param>
public record UpdateUserCustomFieldCommand(
    Guid CustomFieldId,
    Guid UserId,
    string Value) : IRequest, ITransactionalCommand;
