using System.Globalization;
using System.Security.Claims;
using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Infrastructure.Services.Identity;

/// <summary>
/// Handles JWT Bearer authentication pipeline events, enforcing access-token claim restrictions
/// and rejecting active tokens belonging to users in <see cref="IRevokedUsersCache"/>
/// (either fully revoked or issued before a role change).
/// </summary>
/// <param name="revokedUsersCache">The cache containing revoked users and stale-token cutoff timestamps.</param>
/// <param name="logger">The logger instance for recording rejected access tokens.</param>
public sealed partial class JwtBearerEventsHandler(
    IRevokedUsersCache revokedUsersCache,
    ILogger<JwtBearerEventsHandler> logger) : JwtBearerEvents
{
    /// <inheritdoc />
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (principal is null)
        {
            context.Fail("Invalid token principal.");
            return;
        }

        if (!JwtTokenValidationHelper.IsValidAccessTokenPrincipal(principal))
        {
            context.Fail("Token is not a valid access token.");
            return;
        }

        if (!principal.TryGetUserId(out Guid userId))
        {
            context.Fail("Token is missing a valid user identifier.");
            return;
        }

        DateTimeOffset? tokenIssuedAt = ExtractTokenIssuedAt(principal, context.SecurityToken);

        if (await revokedUsersCache.IsTokenRevokedAsync(userId, tokenIssuedAt, context.HttpContext.RequestAborted))
        {
            LogRevokedUserAccessTokenRejected(logger, userId);
            context.Fail("User access has been revoked.");
        }
    }

    private static DateTimeOffset? ExtractTokenIssuedAt(ClaimsPrincipal principal, SecurityToken? securityToken)
    {
        return TryParseIssuedAtTicks(principal)
            ?? TryParseUnixIssuedAt(principal)
            ?? ExtractSecurityTokenIssuedAt(securityToken);
    }

    private static DateTimeOffset? TryParseIssuedAtTicks(ClaimsPrincipal principal)
    {
        string? ticksValue = principal.FindFirst(CustomClaimTypes.IssuedAtTicks)?.Value;
        bool isValid = long.TryParse(ticksValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks)
            && ticks >= DateTimeOffset.MinValue.UtcTicks
            && ticks <= DateTimeOffset.MaxValue.UtcTicks;

        return isValid
            ? new DateTimeOffset(ticks, TimeSpan.Zero)
            : null;
    }

    private static DateTimeOffset? TryParseUnixIssuedAt(ClaimsPrincipal principal)
    {
        string? iatValue = principal.FindFirst(JwtRegisteredClaimNames.Iat)?.Value;
        return long.TryParse(iatValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long unixSeconds)
            ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
            : null;
    }

    private static DateTimeOffset? ExtractSecurityTokenIssuedAt(SecurityToken? securityToken)
    {
        return securityToken is JsonWebToken jsonWebToken && jsonWebToken.IssuedAt != DateTime.MinValue
            ? new DateTimeOffset(DateTime.SpecifyKind(jsonWebToken.IssuedAt, DateTimeKind.Utc))
            : null;
    }

    [LoggerMessage(EventId = LogEventIds.RevokedUserAccessTokenRejected, Level = LogLevel.Warning, Message = "Access token rejected because user ID '{UserId}' is present in the revoked users cache.")]
    private static partial void LogRevokedUserAccessTokenRejected(ILogger logger, Guid userId);
}
