using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.User;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Infrastructure.Options;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Infrastructure.Services;

/// <summary>
/// Generates and validates JWT tokens and secure refresh tokens.
/// </summary>
public sealed partial class TokenService : ITokenService
{
    private static readonly JwtSecurityTokenHandler TokenHandler = new();
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<TokenService> _logger;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly SigningCredentials _signingCredentials;

    public TokenService(IOptions<JwtOptions> jwtOptions, ILogger<TokenService> logger)
    {
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret));
        _signingCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
    }

    /// <inheritdoc />
    public TokenResult GenerateAccessToken(UserBasicDto user, IList<Guid> roleIds)
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
        var tokenString = GenerateTokenString(claims, expiresAt);

        LogAccessTokenGenerated(_logger, user.Id);

        return new TokenResult(tokenString, expiresAt);
    }

    /// <inheritdoc />
    public TokenResult GenerateRefreshToken()
    {
        byte[] randomNumber = RandomNumberGenerator.GetBytes(TokenConstants.RefreshTokenBytesLength);
        string token = WebEncoders.Base64UrlEncode(randomNumber);
        var expiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpiryDays);

        LogRefreshTokenGenerated(_logger);

        return new TokenResult(token, expiresAt);
    }

    /// <inheritdoc />
    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = GetTokenValidationParameters(validateLifetime: false);

        try
        {
            var principal = TokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);

            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
            {
                LogInvalidExpiredToken(_logger, "Invalid token algorithm.");
                return null;
            }

            return principal;
        }
        catch (Exception ex)
        {
            LogExpiredTokenValidationFailed(_logger, ex.Message);
            return null;
        }
    }

    /// <inheritdoc />
    public TokenResultDto GenerateInvitationToken(GenerateInvitationDto dto)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, dto.Email),
            new(CustomClaimTypes.InviteType, CustomClaimTypes.RegistrationInviteValue),
            new(CustomClaimTypes.NgoId, dto.NgoId.ToString()),
        };

        claims.AddRange(dto.RoleIds.Select(roleId => new Claim(CustomClaimTypes.RoleId, roleId.ToString())));

        var expiresAt = DateTime.UtcNow.AddHours(_jwtOptions.InvitationTokenExpiryHours);
        var tokenString = GenerateTokenString(claims, expiresAt);

        LogInvitationTokenGenerated(_logger, dto.Email);

        return new TokenResultDto(tokenString, expiresAt);
    }

    /// <inheritdoc />
    public Task<ClaimsPrincipal?> ValidateInvitationTokenAsync(string token) =>
        ValidateCustomTokenAsync(
            token,
            identity => identity.HasClaim(CustomClaimTypes.InviteType, CustomClaimTypes.RegistrationInviteValue),
            "Token does not contain the required registration invite claims.");

    /// <inheritdoc />
    public TokenResultDto GenerateRestorePasswordToken(PasswordResetTokenDto dto)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, dto.UserId.ToString()),
            new Claim(CustomClaimTypes.PasswordResetToken, dto.PasswordResetToken),
        };

        var expiresAt = DateTime.UtcNow.AddHours(_jwtOptions.ResetPasswordTokenExpiryHours);
        var tokenString = GenerateTokenString(claims, expiresAt);

        LogPasswordResetTokenGenerated(_logger, dto.UserId);

        return new TokenResultDto(tokenString, expiresAt);
    }

    /// <inheritdoc />
    public Task<ClaimsPrincipal?> ValidateRestorePasswordTokenAsync(string token) =>
        ValidateCustomTokenAsync(
            token,
            identity => identity.HasClaim(x => x.Type == CustomClaimTypes.PasswordResetToken),
            "Token does not contain the required password reset claim.");

    private string GenerateTokenString(IEnumerable<Claim> claims, DateTime expiresAt)
    {
        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: _signingCredentials);

        return TokenHandler.WriteToken(token);
    }

    private async Task<ClaimsPrincipal?> ValidateCustomTokenAsync(
        string token,
        Func<ClaimsIdentity, bool> claimRequirement,
        string requirementFailureMessage)
    {
        var tokenValidationParameters = GetTokenValidationParameters(validateLifetime: true);

        try
        {
            var validationResult = await TokenHandler.ValidateTokenAsync(token, tokenValidationParameters);

            if (!validationResult.IsValid)
            {
                LogTokenValidationFailed(_logger, "Token validation failed according to validation parameters.");
                return null;
            }

            if (!claimRequirement(validationResult.ClaimsIdentity))
            {
                LogTokenValidationFailed(_logger, requirementFailureMessage);
                return null;
            }

            return new ClaimsPrincipal(validationResult.ClaimsIdentity);
        }
        catch (Exception ex)
        {
            LogTokenValidationFailed(_logger, ex.Message);
            return null;
        }
    }

    private TokenValidationParameters GetTokenValidationParameters(bool validateLifetime)
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
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

    [LoggerMessage(EventId = LogEventIds.RefreshTokenGenerated, Level = LogLevel.Debug, Message = "Successfully generated secure refresh token.")]
    private static partial void LogRefreshTokenGenerated(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.InvitationTokenGenerated, Level = LogLevel.Information, Message = "Successfully generated invitation token for email '{Email}'.")]
    private static partial void LogInvitationTokenGenerated(ILogger logger, string email);

    [LoggerMessage(EventId = LogEventIds.PasswordResetTokenGenerated, Level = LogLevel.Information, Message = "Successfully generated password reset token for user ID '{UserId}'.")]
    private static partial void LogPasswordResetTokenGenerated(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.InvalidExpiredToken, Level = LogLevel.Warning, Message = "Expired token validation failed: {Reason}")]
    private static partial void LogInvalidExpiredToken(ILogger logger, string reason);

    [LoggerMessage(EventId = LogEventIds.TokenValidationFailed, Level = LogLevel.Warning, Message = "Token validation failed. Reason: {Error}")]
    private static partial void LogTokenValidationFailed(ILogger logger, string error);

    [LoggerMessage(EventId = LogEventIds.ExpiredTokenValidationFailed, Level = LogLevel.Warning, Message = "An error occurred while validating an expired token. Reason: {Error}")]
    private static partial void LogExpiredTokenValidationFailed(ILogger logger, string error);
}
