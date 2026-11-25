using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.Users;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Domain.Enums;
using Crm.Infrastructure.Consts;
using Crm.Infrastructure.Options;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Infrastructure.Services.Identity;

/// <summary>
/// Generates and validates JWT access tokens, cryptographic refresh tokens, and short opaque action tokens.
/// </summary>
public sealed partial class TokenService : ITokenService
{
    private static readonly JsonWebTokenHandler TokenHandler = new()
    {
        MapInboundClaims = false,
    };

    private readonly JwtOptions _jwtOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TokenService> _logger;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly SigningCredentials _signingCredentials;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenService"/> class.
    /// </summary>
    /// <param name="jwtOptions">The JWT signing and expiration configuration.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="timeProvider">The time provider used for both token issuance and expiration validation.</param>
    public TokenService(
        IOptions<JwtOptions> jwtOptions,
        ILogger<TokenService> logger,
        TimeProvider timeProvider)
    {
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
        _timeProvider = timeProvider;
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
            new(CustomClaimTypes.TokenType, CustomClaimTypes.AccessTokenValue),
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        claims.AddRange(roleIds.Select(roleId => new Claim(CustomClaimTypes.RoleId, roleId.ToString())));

        var expiresAt = _timeProvider.GetUtcNow().AddMinutes(_jwtOptions.AccessTokenExpiryMinutes);
        var tokenString = GenerateTokenString(claims, expiresAt.UtcDateTime);

        LogAccessTokenGenerated(_logger, user.Id);

        return new TokenResult(tokenString, expiresAt);
    }

    /// <inheritdoc />
    public TokenResult GenerateRefreshToken()
    {
        string token = GenerateOpaqueToken(TokenConstants.RefreshTokenBytesLength);
        var expiresAt = _timeProvider.GetUtcNow().AddDays(_jwtOptions.RefreshTokenExpiryDays);

        LogRefreshTokenGenerated(_logger);

        return new TokenResult(token, expiresAt);
    }

    /// <inheritdoc />
    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = JwtTokenValidationHelper.CreateValidationParameters(
            _jwtOptions,
            _signingKey,
            validateLifetime: false);

        // Symmetric key validation without external OIDC metadata completes synchronously in-memory.
        return ValidateTokenCoreAsync(
            token,
            tokenValidationParameters,
            identity => JwtTokenValidationHelper.IsValidAccessTokenPrincipal(new ClaimsPrincipal(identity)),
            "Token is not an access token.",
            onValidationError: reason => LogExpiredTokenValidationFailed(_logger, reason),
            onInvalidToken: reason => LogInvalidExpiredToken(_logger, reason))
            .GetAwaiter()
            .GetResult();
    }

    /// <inheritdoc />
    public TokenResultDto GenerateActionToken(ActionTokenType tokenType)
    {
        int expiryHours = tokenType switch
        {
            ActionTokenType.Invitation => _jwtOptions.InvitationTokenExpiryHours,
            ActionTokenType.PasswordReset => _jwtOptions.ResetPasswordTokenExpiryHours,
            _ => throw new ArgumentOutOfRangeException(nameof(tokenType), tokenType, "Unsupported action token type."),
        };

        string token = GenerateOpaqueToken(TokenConstants.ActionTokenBytesLength);
        DateTime expiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddHours(expiryHours);

        LogActionTokenGenerated(_logger, tokenType);

        return new TokenResultDto(token, expiresAt);
    }

    private static string GenerateOpaqueToken(int byteLength)
        => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(byteLength));

    private string GenerateTokenString(IEnumerable<Claim> claims, DateTime expiresAt)
    {
        DateTime utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = utcNow,
            IssuedAt = utcNow,
            Expires = expiresAt,
            SigningCredentials = _signingCredentials,
        };

        return TokenHandler.CreateToken(descriptor);
    }

    private static async Task<ClaimsPrincipal?> ValidateTokenCoreAsync(
        string token,
        TokenValidationParameters validationParameters,
        Func<ClaimsIdentity, bool> claimRequirement,
        string requirementFailureMessage,
        Action<string> onValidationError,
        Action<string> onInvalidToken,
        Action<string>? onException = null)
    {
        try
        {
            var validationResult = await TokenHandler.ValidateTokenAsync(token, validationParameters);

            if (!validationResult.IsValid)
            {
                onValidationError(validationResult.Exception?.Message ?? "Token validation failed.");
                return null;
            }

            if (validationResult.SecurityToken is not JsonWebToken jsonWebToken
                || !string.Equals(jsonWebToken.Alg, SecurityAlgorithms.HmacSha256, StringComparison.Ordinal))
            {
                onInvalidToken("Invalid token algorithm.");
                return null;
            }

            if (!claimRequirement(validationResult.ClaimsIdentity))
            {
                onInvalidToken(requirementFailureMessage);
                return null;
            }

            return new ClaimsPrincipal(validationResult.ClaimsIdentity);
        }
        catch (Exception ex)
        {
            (onException ?? onValidationError)(ex.Message);
            return null;
        }
    }

    [LoggerMessage(EventId = LogEventIds.AccessTokenGenerated, Level = LogLevel.Information, Message = "Successfully generated minimalistic access token for user ID '{UserId}'.")]
    private static partial void LogAccessTokenGenerated(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.RefreshTokenGenerated, Level = LogLevel.Debug, Message = "Successfully generated secure refresh token.")]
    private static partial void LogRefreshTokenGenerated(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.ActionTokenGenerated, Level = LogLevel.Debug, Message = "Successfully generated opaque action token of type '{TokenType}'.")]
    private static partial void LogActionTokenGenerated(ILogger logger, ActionTokenType tokenType);

    [LoggerMessage(EventId = LogEventIds.InvalidExpiredToken, Level = LogLevel.Warning, Message = "Expired token validation failed: {Reason}")]
    private static partial void LogInvalidExpiredToken(ILogger logger, string reason);

    [LoggerMessage(EventId = LogEventIds.ExpiredTokenValidationFailed, Level = LogLevel.Warning, Message = "An error occurred while validating an expired token. Reason: {Error}")]
    private static partial void LogExpiredTokenValidationFailed(ILogger logger, string error);
}
