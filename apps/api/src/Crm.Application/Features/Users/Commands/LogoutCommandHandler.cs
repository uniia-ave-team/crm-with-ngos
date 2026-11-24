using Crm.Application.Common.Consts;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="LogoutCommand"/> to securely log out a user
/// by revoking their active refresh token in the database.
/// </summary>
public sealed partial class LogoutCommandHandler(
    IUserRefreshTokenRepository userRefreshTokenRepository,
    ILogger<LogoutCommandHandler> logger) : ICommandHandler<LogoutCommand>
{
    public async ValueTask<Unit> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        LogInitiatingLogout(logger, command.UserId);

        await userRefreshTokenRepository.RemoveTokenAsync(command.RefreshToken, cancellationToken);

        LogLogoutSuccessful(logger, command.UserId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.InitiatingLogout, Level = LogLevel.Information, Message = "Initiating logout for user ID: {UserId}")]
    private static partial void LogInitiatingLogout(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.LogoutSuccessful, Level = LogLevel.Information, Message = "User with ID: {UserId} successfully logged out. Refresh token revoked.")]
    private static partial void LogLogoutSuccessful(ILogger logger, Guid userId);
}
