using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User.Commands;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="LogoutCommand"/> to securely log out a user
/// by revoking their active refresh token in the database.
/// </summary>
public partial class LogoutCommandHandler(
    IUserRefreshTokenRepository userRefreshTokenRepository,
    ILogger<LogoutCommandHandler> logger) : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        LogInitiatingLogout(logger, request.UserId);

        await userRefreshTokenRepository.RemoveTokenAsync(request.RefreshToken, cancellationToken);

        LogLogoutSuccessful(logger, request.UserId);
    }

    [LoggerMessage(EventId = LogEventIds.InitiatingLogout, Level = LogLevel.Information, Message = "Initiating logout for user ID: {UserId}")]
    private static partial void LogInitiatingLogout(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.LogoutSuccessful, Level = LogLevel.Information, Message = "User with ID: {UserId} successfully logged out. Refresh token revoked.")]
    private static partial void LogLogoutSuccessful(ILogger logger, Guid userId);
}
