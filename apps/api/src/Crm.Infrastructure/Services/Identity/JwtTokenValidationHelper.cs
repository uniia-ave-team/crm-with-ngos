using System.Security.Claims;
using System.Text;
using Crm.Domain.Constants;
using Crm.Infrastructure.Options;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Infrastructure.Services.Identity;

/// <summary>
/// Centralizes JWT validation parameter construction and access-token claim verification
/// shared between ASP.NET Core JWT Bearer authentication and <see cref="TokenService"/>.
/// </summary>
internal static class JwtTokenValidationHelper
{
    /// <summary>
    /// Creates <see cref="TokenValidationParameters"/> configured for HMAC-SHA256 validation
    /// with optional <see cref="TimeProvider"/>-backed lifetime evaluation.
    /// </summary>
    /// <param name="jwtOptions">The JWT configuration options.</param>
    /// <param name="signingKey">Optional pre-constructed symmetric signing key.</param>
    /// <param name="validateLifetime">Whether to enforce <c>nbf</c> and <c>exp</c> claims.</param>
    /// <param name="timeProvider">Optional time provider used when evaluating token lifetimes.</param>
    /// <returns>A configured <see cref="TokenValidationParameters"/> instance.</returns>
    public static TokenValidationParameters CreateValidationParameters(
        JwtOptions jwtOptions,
        SecurityKey? signingKey = null,
        bool validateLifetime = true,
        TimeProvider? timeProvider = null)
    {
        SecurityKey key = signingKey ?? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret));

        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = validateLifetime,
            ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        };

        if (validateLifetime && timeProvider is not null)
        {
            parameters.LifetimeValidator = (notBefore, expires, _, _) =>
            {
                DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;

                return (!notBefore.HasValue || notBefore.Value.ToUniversalTime() <= utcNow)
                    && expires.HasValue
                    && expires.Value.ToUniversalTime() > utcNow;
            };
        }

        return parameters;
    }

    /// <summary>
    /// Determines whether the validated <paramref name="principal"/> represents a legitimate authentication access token
    /// rather than a specialized invitation or password-reset token.
    /// </summary>
    /// <param name="principal">The claims principal extracted from a validated JWT.</param>
    /// <returns><see langword="true"/> if the token is a valid access token; otherwise, <see langword="false"/>.</returns>
    public static bool IsValidAccessTokenPrincipal(ClaimsPrincipal principal)
    {
        string? tokenType = principal.FindFirst(CustomClaimTypes.TokenType)?.Value;

        return (tokenType is null || string.Equals(tokenType, CustomClaimTypes.AccessTokenValue, StringComparison.Ordinal))
            && !principal.HasClaim(c => c.Type is CustomClaimTypes.PasswordResetToken or CustomClaimTypes.InviteType);
    }
}
