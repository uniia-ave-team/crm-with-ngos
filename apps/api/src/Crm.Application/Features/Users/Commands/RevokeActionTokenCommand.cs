using Crm.Application.Common.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Command to permanently revoke an active user action token by its identifier and category.
/// </summary>
/// <param name="Id">The unique identifier of the action token to revoke.</param>
/// <param name="Type">The expected category of the action token.</param>
public record RevokeActionTokenCommand(
    Guid Id,
    ActionTokenType Type) : ICommand, ITransactionalCommand;
