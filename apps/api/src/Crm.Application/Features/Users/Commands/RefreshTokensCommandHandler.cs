using System.Security.Authentication;
using System.Security.Claims;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.User.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="RefreshTokensCommand"/> to issue a new pair of access and refresh tokens.
/// </summary>
public partial class RefreshTokensCommandHandler(
    UserManager<AuthUser> userManager,
    IUserRepository userRepository,
    IAuthRoleRepository roleRepository,
    ITokenService tokenService,
    ILogger<RefreshTokensCommandHandler> logger) : IRequestHandler<RefreshTokensCommand, AuthTokensDto>
{
    public async Task<AuthTokensDto> Handle(RefreshTokensCommand request, CancellationToken cancellationToken)
    {
        LogInitiatingTokenRefresh(logger);

        var principal = ExtractPrincipalFromToken(request.AccessToken);
        var userId = ExtractUserId(principal);

        var user = await GetAndValidateUserAsync(userId, cancellationToken);

        ValidateRefreshToken(user, request.RefreshToken);

        var tokens = await GenerateAndPersistNewTokensAsync(user, cancellationToken);

        LogTokenRefreshSuccessful(logger, userId);

        return tokens;
    }

    private ClaimsPrincipal ExtractPrincipalFromToken(string accessToken)
    {
        var principal = tokenService.GetPrincipalFromExpiredToken(accessToken);

        if (principal == null)
        {
            LogInvalidAccessTokenProvided(logger);
            throw new InvalidCredentialException("Invalid access token.");
        }

        return principal;
    }

    private Guid ExtractUserId(ClaimsPrincipal principal)
    {
        string? userIdString = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
        {
            LogMissingUserIdClaim(logger);
            throw new InvalidCredentialException("Invalid token claims.");
        }

        return userId;
    }

    private async Task<AuthUser> GetAndValidateUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user == null)
        {
            LogUserNotFound(logger, userId);
            throw new InvalidCredentialException("User not found.");
        }

        if (!await userRepository.IsActiveAsync(user.Id, cancellationToken))
        {
            LogUserDeactivated(logger, userId);
            throw new InvalidCredentialException("This user account is deactivated.");
        }

        return user;
    }

    private void ValidateRefreshToken(AuthUser user, string providedRefreshToken)
    {
        if (user.RefreshToken != providedRefreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            LogInvalidOrExpiredRefreshToken(logger, user.Id);
            throw new InvalidCredentialException("Invalid or expired refresh token.");
        }
    }

    private async Task<AuthTokensDto> GenerateAndPersistNewTokensAsync(AuthUser user, CancellationToken cancellationToken)
    {
        var roleIds = await roleRepository.GetRoleIdsByUserAsync(user.Id, cancellationToken);

        var newAccessToken = tokenService.GenerateAccessToken(user, roleIds);
        var newRefreshToken = tokenService.GenerateRefreshToken();

        user.RefreshToken = newRefreshToken.Token;
        user.RefreshTokenExpiryTime = newRefreshToken.ExpiresAt;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            string errors = string.Join(" | ", updateResult.Errors.Select(e => e.Description));
            LogTokenUpdateFailed(logger, user.Id, errors);
            throw new InvalidOperationException($"Failed to update tokens: {errors}");
        }

        return new AuthTokensDto(newAccessToken.Token, newRefreshToken.Token);
    }

    [LoggerMessage(EventId = LogEventIds.InitiatingTokenRefresh, Level = LogLevel.Information, Message = "Initiating token refresh process.")]
    private static partial void LogInitiatingTokenRefresh(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.InvalidAccessTokenProvided, Level = LogLevel.Warning, Message = "Token refresh failed: Invalid access token provided.")]
    private static partial void LogInvalidAccessTokenProvided(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.MissingUserIdClaim, Level = LogLevel.Warning, Message = "Token refresh failed: User ID claim is missing from the access token.")]
    private static partial void LogMissingUserIdClaim(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.RefreshTokensCommandHandlerUserNotFound, Level = LogLevel.Warning, Message = "Token refresh failed: User ID {UserId} was not found.")]
    private static partial void LogUserNotFound(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserDeactivated, Level = LogLevel.Warning, Message = "Token refresh failed: User ID {UserId} is deactivated.")]
    private static partial void LogUserDeactivated(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.InvalidOrExpiredRefreshToken, Level = LogLevel.Warning, Message = "Token refresh failed: Refresh token is invalid or expired for user ID {UserId}.")]
    private static partial void LogInvalidOrExpiredRefreshToken(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.TokenUpdateFailed, Level = LogLevel.Error, Message = "Failed to update new tokens in database for user ID {UserId}. Reason: {Errors}")]
    private static partial void LogTokenUpdateFailed(ILogger logger, Guid userId, string errors);

    [LoggerMessage(EventId = LogEventIds.TokenRefreshSuccessful, Level = LogLevel.Information, Message = "Tokens successfully refreshed for user ID: {UserId}")]
    private static partial void LogTokenRefreshSuccessful(ILogger logger, Guid userId);
}
