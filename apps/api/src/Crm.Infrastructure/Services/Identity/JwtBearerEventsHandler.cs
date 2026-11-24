using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;

namespace Crm.Infrastructure.Services.Identity;

/// <summary>
/// Handles JWT Bearer authentication pipeline events, enforcing access-token claim restrictions
/// and rejecting active tokens belonging to users in <see cref="IRevokedUsersCache"/>.
/// </summary>
/// <param name="revokedUsersCache">The cache containing recently deactivated or deleted user identifiers.</param>
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

        if (await revokedUsersCache.IsUserRevokedAsync(userId, context.HttpContext.RequestAborted))
        {
            LogRevokedUserAccessTokenRejected(logger, userId);
            context.Fail("User access has been revoked.");
        }
    }

    [LoggerMessage(EventId = LogEventIds.RevokedUserAccessTokenRejected, Level = LogLevel.Warning, Message = "Access token rejected because user ID '{UserId}' is present in the revoked users cache.")]
    private static partial void LogRevokedUserAccessTokenRejected(ILogger logger, Guid userId);
}
