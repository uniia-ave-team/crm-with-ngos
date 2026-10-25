using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="GenerateRestorePasswordTokenCommand"/> to generate a secure password restore token for a user.
/// </summary>
/// <param name="identityService">The service used to manage user identities and generate identity reset tokens.</param>
/// <param name="tokenService">The service responsible for generating JWT tokens.</param>
/// <param name="logger">The logger used to record the password restore token generation process.</param>
public partial class RestorePasswordCommandHandler(
    IIdentityService identityService,
    ITokenService tokenService,
    ILogger<RestorePasswordCommandHandler> logger) : IRequestHandler<GenerateRestorePasswordTokenCommand, TokenResultDto>
{
    /// <summary>
    /// Executes the password restore token generation process asynchronously.
    /// </summary>
    /// <param name="request">The command containing the user identifier for whom the reset token is requested.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task returning the generated JWT password restore token result.</returns>
    /// <exception cref="NotFoundException">Thrown when the user with the specified ID does not exist.</exception>
    public async Task<TokenResultDto> Handle(GenerateRestorePasswordTokenCommand request, CancellationToken cancellationToken)
    {
        LogGeneratingPasswordResetToken(logger, request.UserId);

        var rawResetToken = await identityService.GetResetTokenAsync(request.UserId, cancellationToken);

        var tokenResult = tokenService.GenerateRestorePasswordToken(new PasswordResetTokenDto(request.UserId, rawResetToken));

        LogPasswordResetTokenGeneratedSuccessfully(logger, request.UserId);

        return tokenResult;
    }

    [LoggerMessage(EventId = LogEventIds.GeneratingPasswordResetToken, Level = LogLevel.Information, Message = "Initiating password restore token generation for user ID: {UserId}")]
    private static partial void LogGeneratingPasswordResetToken(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.PasswordResetTokenGeneratedSuccessfully, Level = LogLevel.Information, Message = "Successfully generated password restore token for user ID: {UserId}")]
    private static partial void LogPasswordResetTokenGeneratedSuccessfully(ILogger logger, Guid userId);
}
