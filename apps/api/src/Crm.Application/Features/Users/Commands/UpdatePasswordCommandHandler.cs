using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.Users;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="UpdatePasswordCommand"/> to securely change a user's password.
/// Changing the password revokes every refresh token of the user, which ends all their other sessions;
/// the session that made the change gets a fresh token pair and stays signed in.
/// </summary>
/// <param name="identityService">The identity service used for user and role management.</param>
/// <param name="authUserRepository">The repository used to read the user's identity record.</param>
/// <param name="roleRepository">The repository used to read the user's role IDs for the new access token.</param>
/// <param name="tokenService">The service that generates the new access and refresh tokens.</param>
/// <param name="logger">The logger used to record the password update process.</param>
public sealed partial class UpdatePasswordCommandHandler(
    IIdentityService identityService,
    IAuthUserRepository authUserRepository,
    IAuthRoleRepository roleRepository,
    ITokenService tokenService,
    ILogger<UpdatePasswordCommandHandler> logger) : ICommandHandler<UpdatePasswordCommand, AuthTokensDto>
{
    public async ValueTask<AuthTokensDto> Handle(UpdatePasswordCommand command, CancellationToken cancellationToken)
    {
        LogUpdatingPassword(logger, command.UserId);

        await identityService.ChangePasswordAsync(command.UserId, command.CurrentPassword, command.NewPassword, cancellationToken);

        var user = await authUserRepository.GetAsync<UserStatusDto>(command.UserId, cancellationToken);
        var roleIds = await roleRepository.GetRoleIdsByUserAsync(user.Id, cancellationToken);

        var accessToken = tokenService.GenerateAccessToken(new UserBasicDto(user.Id, user.Email), roleIds);
        var refreshToken = tokenService.GenerateRefreshToken();

        await identityService.AddRefreshTokenAsync(user.Id, refreshToken, cancellationToken);

        LogPasswordUpdatedSuccessfully(logger, command.UserId);

        return new AuthTokensDto(accessToken.Token, refreshToken.Token);
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingPassword, Level = LogLevel.Information, Message = "Initiating password update for user ID: {UserId}")]
    private static partial void LogUpdatingPassword(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.PasswordUpdatedSuccessfully, Level = LogLevel.Information, Message = "Password successfully updated for user ID: {UserId}")]
    private static partial void LogPasswordUpdatedSuccessfully(ILogger logger, Guid userId);
}
