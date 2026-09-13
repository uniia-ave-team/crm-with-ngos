using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User.Commands;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="LogoutCommand"/> to securely log out a user
/// by revoking their active refresh token in the database.
/// </summary>
public partial class LogoutCommandHandler(
    UserManager<AuthUser> userManager,
    ILogger<LogoutCommandHandler> logger) : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        LogInitiatingLogout(logger, request.UserId);

        var user = await userManager.FindByIdAsync(request.UserId.ToString());

        if (user == null)
        {
            LogUserNotFound(logger, request.UserId);
            throw new EntityNotFoundException(nameof(AuthUser), request.UserId);
        }

        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;

        var result = await userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            string errors = string.Join(" | ", result.Errors.Select(e => e.Description));
            LogLogoutFailed(logger, request.UserId, errors);

            throw new InvalidOperationException($"Logout failed while updating user details: {errors}");
        }

        LogLogoutSuccessful(logger, request.UserId);
    }

    [LoggerMessage(EventId = LogEventIds.InitiatingLogout, Level = LogLevel.Information, Message = "Initiating logout for user ID: {UserId}")]
    private static partial void LogInitiatingLogout(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.LogoutCommandHandlerUserNotFound, Level = LogLevel.Warning, Message = "Logout failed. User with ID {UserId} was not found.")]
    private static partial void LogUserNotFound(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.LogoutFailed, Level = LogLevel.Error, Message = "Failed to update user during logout for ID '{UserId}'. Reason: {Errors}")]
    private static partial void LogLogoutFailed(ILogger logger, Guid userId, string errors);

    [LoggerMessage(EventId = LogEventIds.LogoutSuccessful, Level = LogLevel.Information, Message = "User with ID: {UserId} successfully logged out. Refresh token revoked.")]
    private static partial void LogLogoutSuccessful(ILogger logger, Guid userId);
}
