using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.User.Commands;

/// <summary>
/// Represents a command to add a new custom key-value field to a user's profile.
/// </summary>
/// <param name="UserId">The unique identifier of the user to whom the field is added.</param>
/// <param name="Key">The unique key or name of the custom field.</param>
/// <param name="Value">The string value of the custom field.</param>
public record AddUserCustomFieldCommand(
    Guid UserId,
    string Key,
    string Value) : IRequest, ITransactionalCommand;
