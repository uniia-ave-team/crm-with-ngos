using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="RevokeActionTokenCommand"/> to permanently invalidate an active user action token.
/// </summary>
/// <param name="actionTokenService">The service managing user action token lifecycles.</param>
/// <param name="logger">The logger used to record token revocation.</param>
public sealed partial class RevokeActionTokenCommandHandler(
    IUserActionTokenService actionTokenService,
    ILogger<RevokeActionTokenCommandHandler> logger) : ICommandHandler<RevokeActionTokenCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(RevokeActionTokenCommand command, CancellationToken cancellationToken)
    {
        LogRevokingActionToken(logger, command.Type, command.Id);

        await actionTokenService.RevokeByIdAsync(command.Id, command.Type, cancellationToken);

        LogActionTokenRevokedSuccessfully(logger, command.Type, command.Id);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.RevokingActionToken, Level = LogLevel.Information, Message = "Revoking {TokenType} action token with ID: {TokenId}.")]
    private static partial void LogRevokingActionToken(ILogger logger, ActionTokenType tokenType, Guid tokenId);

    [LoggerMessage(EventId = LogEventIds.ActionTokenRevokedSuccessfully, Level = LogLevel.Information, Message = "Successfully revoked {TokenType} action token with ID: {TokenId}.")]
    private static partial void LogActionTokenRevokedSuccessfully(ILogger logger, ActionTokenType tokenType, Guid tokenId);
}
