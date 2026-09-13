using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.User;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Infrastructure.Options;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Infrastructure.Services;

/// <summary>
/// Generates and validates JWT tokens and secure refresh tokens.
/// Follows the Single Responsibility Principle by delegating specific validation logic to reusable configurations.
/// </summary>
public sealed partial class TokenService(
    IOptions<JwtOptions> jwtOptions,
    ILogger<TokenService> logger) : ITokenService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    /// <inheritdoc />
    public TokenResult GenerateAccessToken(AuthUser user, IList<Guid> roleIds)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        claims.AddRange(roleIds.Select(roleId => new Claim(CustomClaimTypes.RoleId, roleId.ToString())));

        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpiryMinutes);
        var token = CreateJwtToken(claims, expiresAt);

        LogAccessTokenGenerated(logger, user.Id);

        return new(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    /// <inheritdoc />
    public TokenResult GenerateRefreshToken()
    {
        byte[] randomNumber = RandomNumberGenerator.GetBytes(32);
        string token = WebEncoders.Base64UrlEncode(randomNumber);

        var expiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpiryDays);

        LogRefreshTokenGenerated(logger);

        return new(token, expiresAt);
    }

    /// <inheritdoc />
    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = GetTokenValidationParameters(validateLifetime: false);
        var tokenHandler = new JwtSecurityTokenHandler();

        try
        {
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                LogInvalidExpiredToken(logger, "Invalid token algorithm.");
                return null;
            }

            return principal;
        }
        catch (Exception ex)
        {
            LogExpiredTokenValidationFailed(logger, ex.Message);
            return null;
        }
    }

    /// <inheritdoc />
    public InvitationTokenResultDto GenerateInvitationToken(GenerateInvitationDto dto)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, dto.Email),
            new(CustomClaimTypes.InviteType, CustomClaimTypes.RegistrationInviteValue),
            new(CustomClaimTypes.NgoId, dto.NgoId.ToString()),
        };

        claims.AddRange(dto.RoleIds.Select(roleId => new Claim(CustomClaimTypes.RoleId, roleId.ToString())));

        var expiresAt = DateTime.UtcNow.AddHours(_jwtOptions.InvitationTokenExpiryHours);

        var token = CreateJwtToken(claims, expiresAt);

        LogInvitationTokenGenerated(logger, dto.Email);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return new InvitationTokenResultDto(tokenString, expiresAt);
    }

    /// <inheritdoc />
    public async Task<ClaimsPrincipal?> ValidateInvitationTokenAsync(string token)
    {
        var tokenValidationParameters = GetTokenValidationParameters(validateLifetime: true);
        var tokenHandler = new JwtSecurityTokenHandler();

        try
        {
            var validationResult = await tokenHandler.ValidateTokenAsync(token, tokenValidationParameters);

            if (!validationResult.IsValid)
            {
                LogInvalidInvitationToken(logger, "Token validation failed according to validation parameters.");
                return null;
            }

            if (!validationResult.ClaimsIdentity.HasClaim(CustomClaimTypes.InviteType, CustomClaimTypes.RegistrationInviteValue))
            {
                LogInvalidInvitationToken(logger, "Token does not contain the required registration invite claims.");
                return null;
            }

            return new ClaimsPrincipal(validationResult.ClaimsIdentity);
        }
        catch (Exception ex)
        {
            LogTokenValidationFailed(logger, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Centralizes the creation of JWT tokens to avoid duplication (DRY principle).
    /// </summary>
    private JwtSecurityToken CreateJwtToken(IEnumerable<Claim> claims, DateTime expires)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        return new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: creds);
    }

    /// <summary>
    /// Generates standard token validation parameters to ensure consistency across validation methods (DRY principle).
    /// </summary>
    private TokenValidationParameters GetTokenValidationParameters(bool validateLifetime)
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret)),
            ValidateIssuer = true,
            ValidIssuer = _jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwtOptions.Audience,
            ValidateLifetime = validateLifetime,
            ClockSkew = TimeSpan.Zero,
        };
    }

    [LoggerMessage(EventId = LogEventIds.AccessTokenGenerated, Level = LogLevel.Information, Message = "Successfully generated minimalistic access token for user ID '{UserId}'.")]
    private static partial void LogAccessTokenGenerated(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.InvitationTokenGenerated, Level = LogLevel.Information, Message = "Successfully generated invitation token for email '{Email}'.")]
    private static partial void LogInvitationTokenGenerated(ILogger logger, string email);

    [LoggerMessage(EventId = LogEventIds.InvalidInvitationToken, Level = LogLevel.Warning, Message = "Invitation token validation failed: {Reason}")]
    private static partial void LogInvalidInvitationToken(ILogger logger, string reason);

    [LoggerMessage(EventId = LogEventIds.TokenValidationFailed, Level = LogLevel.Warning, Message = "An error occurred during token validation. Reason: {Error}")]
    private static partial void LogTokenValidationFailed(ILogger logger, string error);

    [LoggerMessage(EventId = LogEventIds.RefreshTokenGenerated, Level = LogLevel.Debug, Message = "Successfully generated secure refresh token.")]
    private static partial void LogRefreshTokenGenerated(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.InvalidExpiredToken, Level = LogLevel.Warning, Message = "Expired token validation failed: {Reason}")]
    private static partial void LogInvalidExpiredToken(ILogger logger, string reason);

    [LoggerMessage(EventId = LogEventIds.ExpiredTokenValidationFailed, Level = LogLevel.Warning, Message = "An error occurred while validating an expired token. Reason: {Error}")]
    private static partial void LogExpiredTokenValidationFailed(ILogger logger, string error);
}
