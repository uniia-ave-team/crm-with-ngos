using System.Security.Authentication;
using Crm.Application.Common.Consts;
using Crm.Application.Exceptions;
using Crm.Application.Features.Users.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="ResetPasswordCommand"/> to reset a user's password using a persisted single-use opaque token.
/// </summary>
/// <param name="actionTokenService">The service used to validate and atomically consume password reset tokens.</param>
/// <param name="identityService">The service used to manage user identities and reset passwords.</param>
/// <param name="logger">The logger used to record the password reset process.</param>
public sealed partial class ResetPasswordCommandHandler(
    IUserActionTokenService actionTokenService,
    IIdentityService identityService,
    ILogger<ResetPasswordCommandHandler> logger) : ICommandHandler<ResetPasswordCommand>
{
    /// <summary>
    /// Executes the password reset process asynchronously.
    /// </summary>
    /// <param name="command">The command containing the opaque reset token and the new password.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidCredentialException">Thrown when the token is invalid, revoked, or expired.</exception>
    /// <exception cref="NotFoundException">Thrown when the user referenced in the token does not exist.</exception>
    /// <exception cref="UserOperationException">Thrown when the underlying identity provider fails to reset the password.</exception>
    public async ValueTask<Unit> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        LogResettingPassword(logger);

        var resolved = await actionTokenService.ResolveValidPasswordResetAsync(command.Token, cancellationToken);

        if (resolved is null
            || !await actionTokenService.TryConsumeAsync(resolved.Token.Id, ActionTokenType.PasswordReset, cancellationToken))
        {
            LogInvalidPasswordResetToken(logger);
            throw new InvalidCredentialException("The password reset token is invalid or has expired.");
        }

        Guid userId = resolved.Token.TargetUserId!.Value;

        await identityService.ResetPasswordAsync(userId, resolved.Metadata.IdentityResetToken, command.NewPassword, cancellationToken);

        LogPasswordResetSuccessfully(logger, userId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.ResettingPassword, Level = LogLevel.Information, Message = "Initiating password reset attempt.")]
    private static partial void LogResettingPassword(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.InvalidPasswordResetTokenAttempt, Level = LogLevel.Warning, Message = "Password reset failed due to an invalid, revoked, or expired token.")]
    private static partial void LogInvalidPasswordResetToken(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.PasswordResetSuccessfully, Level = LogLevel.Information, Message = "Password has been successfully reset for user ID: '{UserId}'.")]
    private static partial void LogPasswordResetSuccessfully(ILogger logger, Guid userId);
}
