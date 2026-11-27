using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Users;
using Crm.Application.Extensions;
using Crm.Application.Features.Users.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Exceptions;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Handles the <see cref="ValidatePasswordResetTokenQuery"/> to verify a password reset token
/// and return the target user's identifying details (including avatar URL) before submitting a new password.
/// </summary>
/// <param name="actionTokenService">The service used to resolve and authenticate active password reset tokens.</param>
/// <param name="fileUrlProvider">The service used to resolve the target user's avatar URL.</param>
/// <param name="logger">The logger used to record validation attempts.</param>
public sealed partial class ValidatePasswordResetTokenQueryHandler(
    IUserActionTokenService actionTokenService,
    IFileUrlProvider fileUrlProvider,
    ILogger<ValidatePasswordResetTokenQueryHandler> logger)
    : IQueryHandler<ValidatePasswordResetTokenQuery, PasswordResetDetailsDto>
{
    /// <inheritdoc />
    public async ValueTask<PasswordResetDetailsDto> Handle(
        ValidatePasswordResetTokenQuery query,
        CancellationToken cancellationToken)
    {
        LogValidatingPasswordResetToken(logger);

        var resolved = await actionTokenService.ResolveValidPasswordResetAsync(query.Token, cancellationToken);

        if (resolved is null || resolved.Token.TargetFirstName is null || resolved.Token.TargetLastName is null)
        {
            throw new DomainValidationException("Invalid or expired password reset token.");
        }

        string? avatarUrl = fileUrlProvider.ResolveUserAvatarUrl(
            resolved.Token.TargetAvatar,
            resolved.Token.TargetUserId!.Value);

        string maskedEmail = resolved.Token.TargetEmail.MaskEmail();

        LogPasswordResetTokenValidatedSuccessfully(logger, maskedEmail);

        return new PasswordResetDetailsDto(
            resolved.Token.TargetEmail,
            resolved.Token.TargetFirstName,
            resolved.Token.TargetLastName,
            avatarUrl,
            resolved.Token.ExpiresAt);
    }

    [LoggerMessage(EventId = LogEventIds.ValidatingPasswordResetToken, Level = LogLevel.Information, Message = "Validating incoming password reset token.")]
    private static partial void LogValidatingPasswordResetToken(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.PasswordResetTokenValidatedSuccessfully, Level = LogLevel.Information, Message = "Password reset token validated successfully for email: {Email}")]
    private static partial void LogPasswordResetTokenValidatedSuccessfully(ILogger logger, string email);
}
