using System.Security.Authentication;
using System.Security.Claims;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User.Commands;
using Crm.Application.Exceptions;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="ResetPasswordCommand"/> to reset a user's password using a validated token.
/// </summary>
/// <param name="tokenService">The service responsible for validating JWT tokens.</param>
/// <param name="identityService">The service used to manage user identities and reset passwords.</param>
/// <param name="logger">The logger used to record the password reset process.</param>
public partial class ResetPasswordCommandHandler(
    ITokenService tokenService,
    IIdentityService identityService,
    ILogger<ResetPasswordCommandHandler> logger) : IRequestHandler<ResetPasswordCommand>
{
    /// <summary>
    /// Executes the password reset process asynchronously.
    /// </summary>
    /// <param name="request">The command containing the JWT reset token and the new password.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidCredentialException">Thrown when the token is invalid, expired, or missing required claims.</exception>
    /// <exception cref="EntityNotFoundException">Thrown when the user referenced in the token does not exist.</exception>
    /// <exception cref="UserOperationException">Thrown when the underlying identity provider fails to reset the password.</exception>
    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        LogResettingPassword(logger);

        var principal = await tokenService.ValidateRestorePasswordTokenAsync(request.Token);
        if (principal is null)
        {
            LogInvalidPasswordResetToken(logger);
            throw new InvalidCredentialException("The password reset token is invalid or has expired.");
        }

        var userIdString = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var rawResetToken = principal.FindFirst(CustomClaimTypes.PasswordResetToken)?.Value;

        if (!Guid.TryParse(userIdString, out var userId) || string.IsNullOrWhiteSpace(rawResetToken))
        {
            LogInvalidPasswordResetToken(logger);
            throw new InvalidCredentialException("The token does not contain valid user information or reset token.");
        }

        await identityService.ResetPasswordAsync(userId, rawResetToken, request.NewPassword, cancellationToken);

        LogPasswordResetSuccessfully(logger, userId);
    }

    [LoggerMessage(EventId = LogEventIds.ResettingPassword, Level = LogLevel.Information, Message = "Initiating password reset attempt.")]
    private static partial void LogResettingPassword(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.InvalidPasswordResetTokenAttempt, Level = LogLevel.Warning, Message = "Password reset failed due to an invalid or malformed token.")]
    private static partial void LogInvalidPasswordResetToken(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.PasswordResetSuccessfully, Level = LogLevel.Information, Message = "Password has been successfully reset for user ID: '{UserId}'.")]
    private static partial void LogPasswordResetSuccessfully(ILogger logger, Guid userId);
}
