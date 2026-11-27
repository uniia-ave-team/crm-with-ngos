using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Users;
using Crm.Application.Extensions;
using Crm.Application.Features.Users.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Handles the <see cref="ValidateInvitationTokenQuery"/> to validate an opaque invitation token against persisted storage.
/// </summary>
/// <param name="actionTokenService">The service used to resolve and authenticate active invitation tokens.</param>
/// <param name="authUserRepository">The repository used to verify that no user already exists for the invited email.</param>
/// <param name="logger">The logger used to record the validation process.</param>
public sealed partial class ValidateInvitationTokenQueryHandler(
    IUserActionTokenService actionTokenService,
    IAuthUserRepository authUserRepository,
    ILogger<ValidateInvitationTokenQueryHandler> logger) : IQueryHandler<ValidateInvitationTokenQuery, InvitationDetailsDto>
{
    /// <inheritdoc />
    public async ValueTask<InvitationDetailsDto> Handle(ValidateInvitationTokenQuery query, CancellationToken cancellationToken)
    {
        LogValidatingToken(logger);

        var resolved = await actionTokenService.ResolveValidInvitationAsync(query.Token, cancellationToken);

        await authUserRepository.EnsureDoesNotExistAsync(resolved.Token.TargetEmail, cancellationToken);

        string maskedEmail = resolved.Token.TargetEmail.MaskEmail();

        LogTokenValidatedSuccessfully(logger, maskedEmail);

        return new InvitationDetailsDto(
            resolved.Token.TargetEmail,
            resolved.Metadata.NgoId,
            resolved.Metadata.RoleIds);
    }

    [LoggerMessage(EventId = LogEventIds.ValidatingToken, Level = LogLevel.Information, Message = "Validating incoming invitation token.")]
    private static partial void LogValidatingToken(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.TokenValidatedSuccessfully, Level = LogLevel.Information, Message = "Invitation token validated successfully for email: {Email}")]
    private static partial void LogTokenValidatedSuccessfully(ILogger logger, string email);
}
