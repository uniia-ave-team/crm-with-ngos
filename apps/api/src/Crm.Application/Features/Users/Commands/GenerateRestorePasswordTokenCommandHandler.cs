using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Users;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="GenerateRestorePasswordTokenCommand"/> to generate and persist a short opaque password reset token for a user.
/// </summary>
/// <param name="identityService">The service used to load the user and generate the ASP.NET Identity password reset token in a single query.</param>
/// <param name="actionTokenService">The service responsible for issuing encrypted opaque action tokens.</param>
/// <param name="logger">The logger used to record the password restore token generation process.</param>
public sealed partial class GenerateRestorePasswordTokenCommandHandler(
    IIdentityService identityService,
    IUserActionTokenService actionTokenService,
    ILogger<GenerateRestorePasswordTokenCommandHandler> logger) : ICommandHandler<GenerateRestorePasswordTokenCommand, TokenResultDto>
{
    /// <summary>
    /// Executes the password restore token generation process asynchronously.
    /// </summary>
    /// <param name="command">The command containing the user identifier for whom the reset token is requested.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task returning the generated opaque password restore token and its expiration timestamp.</returns>
    /// <exception cref="NotFoundException">Thrown when the user with the specified ID does not exist.</exception>
    public async ValueTask<TokenResultDto> Handle(GenerateRestorePasswordTokenCommand command, CancellationToken cancellationToken)
    {
        LogGeneratingPasswordResetToken(logger, command.UserId);

        IdentityPasswordResetTokenResult resetIdentity = await identityService.GetResetTokenAsync(command.UserId, cancellationToken);

        TokenResultDto tokenResult = await actionTokenService.IssueAsync(
            ActionTokenType.PasswordReset,
            resetIdentity.Email,
            new PasswordResetTokenMetadata(resetIdentity.ResetToken),
            targetUserId: command.UserId,
            createdBy: command.CurrentUserId,
            cancellationToken: cancellationToken);

        LogPasswordResetTokenGeneratedSuccessfully(logger, command.UserId);

        return tokenResult;
    }

    [LoggerMessage(EventId = LogEventIds.GeneratingPasswordResetToken, Level = LogLevel.Information, Message = "Initiating password restore token generation for user ID: {UserId}")]
    private static partial void LogGeneratingPasswordResetToken(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.PasswordResetTokenGeneratedSuccessfully, Level = LogLevel.Information, Message = "Successfully generated password restore token for user ID: {UserId}")]
    private static partial void LogPasswordResetTokenGeneratedSuccessfully(ILogger logger, Guid userId);
}
