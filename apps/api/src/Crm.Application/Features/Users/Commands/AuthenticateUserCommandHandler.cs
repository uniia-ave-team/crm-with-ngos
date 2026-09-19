using System.Security.Authentication;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.User.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="AuthenticateUserCommand"/> to verify user credentials and issue a JWT access token.
/// </summary>
public partial class AuthenticateUserCommandHandler(
    UserManager<AuthUser> userManager,
    IUserRepository userRepository,
    IAuthRoleRepository roleRepository,
    ITokenService tokenService,
    ILogger<AuthenticateUserCommandHandler> logger) : IRequestHandler<AuthenticateUserCommand, AuthTokensDto>
{
    public async Task<AuthTokensDto> Handle(AuthenticateUserCommand request, CancellationToken cancellationToken)
    {
        LogAuthenticatingUser(logger, request.Email);

        var user = await userManager.FindByEmailAsync(request.Email);

        if (user == null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            LogAuthenticationFailed(logger, request.Email);
            throw new InvalidCredentialException("Invalid email or password.");
        }

        if (!await userRepository.IsActiveAsync(user.Id, cancellationToken))
        {
            LogAuthenticationFailed(logger, request.Email);
            throw new InvalidCredentialException("This user account is deactivated.");
        }

        var roleIds = await roleRepository.GetRoleIdsByUserAsync(user.Id, cancellationToken);

        var accessToken = tokenService.GenerateAccessToken(user, roleIds);

        var refreshToken = tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken.Token;
        user.RefreshTokenExpiryTime = refreshToken.ExpiresAt;

        await userManager.UpdateAsync(user);

        LogUserAuthenticatedSuccessfully(logger, request.Email, user.Id);

        return new AuthTokensDto(accessToken.Token, refreshToken.Token);
    }

    [LoggerMessage(EventId = LogEventIds.AuthenticatingUser, Level = LogLevel.Information, Message = "Authenticating user with email: {Email}")]
    private static partial void LogAuthenticatingUser(ILogger logger, string email);

    [LoggerMessage(EventId = LogEventIds.AuthenticationFailed, Level = LogLevel.Warning, Message = "Authentication failed for email: {Email}. Invalid credentials.")]
    private static partial void LogAuthenticationFailed(ILogger logger, string email);

    [LoggerMessage(EventId = LogEventIds.UserAuthenticatedSuccessfully, Level = LogLevel.Information, Message = "User '{Email}' successfully authenticated with ID: {UserId}")]
    private static partial void LogUserAuthenticatedSuccessfully(ILogger logger, string email, Guid userId);
}
