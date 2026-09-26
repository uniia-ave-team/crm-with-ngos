using System.Security.Authentication;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Commands;
using Crm.Application.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="AuthenticateUserCommand"/> to verify user credentials and issue a JWT access token.
/// </summary>
public partial class AuthenticateUserCommandHandler(
    IIdentityService identityService,
    IAuthRoleRepository roleRepository,
    ITokenService tokenService,
    IAuthUserRepository authUserRepository,
    ILogger<AuthenticateUserCommandHandler> logger) : IRequestHandler<AuthenticateUserCommand, AuthTokensDto>
{
    public async Task<AuthTokensDto> Handle(AuthenticateUserCommand request, CancellationToken cancellationToken)
    {
        var maskedEmail = request.Email.MaskEmail();

        LogAuthenticatingUser(logger, maskedEmail);

        var isPasswordValid = await identityService.CheckPasswordAsync(request.Email, request.Password, cancellationToken);

        var user = await authUserRepository.GetAsync<UserStatusDto>(request.Email, cancellationToken);

        if (!isPasswordValid || !user.IsActive)
        {
            LogAuthenticationFailed(logger, maskedEmail);
            throw new InvalidCredentialException("Invalid email or password.");
        }

        var roleIds = await roleRepository.GetRoleIdsByUserAsync(user.Id, cancellationToken);

        var accessToken = tokenService.GenerateAccessToken(user.Adapt<UserBasicDto>(), roleIds);

        var refreshToken = tokenService.GenerateRefreshToken();

        await identityService.AddRefreshTokenAsync(user.Id, refreshToken, cancellationToken);

        LogUserAuthenticatedSuccessfully(logger, maskedEmail, user.Id);

        return new AuthTokensDto(accessToken.Token, refreshToken.Token);
    }

    [LoggerMessage(EventId = LogEventIds.AuthenticatingUser, Level = LogLevel.Information, Message = "Authenticating user with email: {Email}")]
    private static partial void LogAuthenticatingUser(ILogger logger, string email);

    [LoggerMessage(EventId = LogEventIds.AuthenticationFailed, Level = LogLevel.Warning, Message = "Authentication failed for email: {Email}. Invalid credentials.")]
    private static partial void LogAuthenticationFailed(ILogger logger, string email);

    [LoggerMessage(EventId = LogEventIds.UserAuthenticatedSuccessfully, Level = LogLevel.Information, Message = "User '{Email}' successfully authenticated with ID: {UserId}")]
    private static partial void LogUserAuthenticatedSuccessfully(ILogger logger, string email, Guid userId);
}
