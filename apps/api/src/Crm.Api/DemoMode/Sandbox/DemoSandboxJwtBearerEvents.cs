using System.Globalization;
using System.Security.Claims;
using Crm.Domain.Constants;
using Crm.Infrastructure.Services.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Api.DemoMode.Sandbox;

/// <summary>
/// Runs the regular <see cref="JwtBearerEventsHandler"/> and then rejects the access tokens that were revoked
/// inside the token's own sandbox (see <see cref="DemoSandboxRevokedUsersCache"/>): all tokens of a user who was
/// deactivated or deleted there, and the tokens a user received before their roles changed there.
/// Authentication runs before <see cref="DemoModeMiddleware"/> picks the request's sandbox, so the sandbox is
/// looked up here from the <c>demo_sid</c> claim. A token whose sandbox no longer exists passes this check:
/// the middleware answers it with <see cref="DemoErrorCodes.SessionExpired"/>.
/// </summary>
/// <param name="inner">The regular JWT bearer events.</param>
/// <param name="sandboxManager">The sandbox manager.</param>
/// <param name="logger">The logger.</param>
public sealed partial class DemoSandboxJwtBearerEvents(
    JwtBearerEventsHandler inner,
    DemoSandboxManager sandboxManager,
    ILogger<DemoSandboxJwtBearerEvents> logger) : JwtBearerEvents
{
    /// <inheritdoc />
    public override Task MessageReceived(MessageReceivedContext context) => inner.MessageReceived(context);

    /// <inheritdoc />
    public override Task AuthenticationFailed(AuthenticationFailedContext context) => inner.AuthenticationFailed(context);

    /// <inheritdoc />
    public override Task Challenge(JwtBearerChallengeContext context) => inner.Challenge(context);

    /// <inheritdoc />
    public override Task Forbidden(ForbiddenContext context) => inner.Forbidden(context);

    /// <inheritdoc />
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        await inner.TokenValidated(context);

        var principal = context.Principal;

        // Already rejected by the regular checks.
        if (context.Result is not null || principal is null)
        {
            return;
        }

        string? sandboxId = principal.FindFirstValue(DemoClaimTypes.SandboxId);
        string? subject = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue(CustomClaimTypes.Sub);

        if (sandboxManager.TryGet(sandboxId, out var sandbox)
            && Guid.TryParse(subject, out Guid userId)
            && sandbox.IsTokenRevoked(userId, ReadIssuedAt(principal, context.SecurityToken)))
        {
            LogRevokedUserRejected(logger, userId, sandbox.Id);
            context.Fail("User access has been revoked.");
        }
    }

    /// <summary>
    /// When the token was issued, read the way <see cref="JwtBearerEventsHandler"/> reads it: the exact
    /// <c>iat_ticks</c> claim, else the <c>iat</c> claim (whole seconds), else the issue time of the token itself.
    /// </summary>
    private static DateTimeOffset? ReadIssuedAt(ClaimsPrincipal principal, SecurityToken? securityToken)
        => ReadIssuedAtTicks(principal) ?? ReadUnixIssuedAt(principal) ?? ReadSecurityTokenIssuedAt(securityToken);

    private static DateTimeOffset? ReadIssuedAtTicks(ClaimsPrincipal principal)
    {
        string? ticksValue = principal.FindFirst(CustomClaimTypes.IssuedAtTicks)?.Value;
        bool isValid = long.TryParse(ticksValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks)
            && ticks >= DateTimeOffset.MinValue.UtcTicks
            && ticks <= DateTimeOffset.MaxValue.UtcTicks;

        return isValid ? new DateTimeOffset(ticks, TimeSpan.Zero) : null;
    }

    private static DateTimeOffset? ReadUnixIssuedAt(ClaimsPrincipal principal)
    {
        string? iatValue = principal.FindFirst(JwtRegisteredClaimNames.Iat)?.Value;

        return long.TryParse(iatValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long unixSeconds)
            ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
            : null;
    }

    private static DateTimeOffset? ReadSecurityTokenIssuedAt(SecurityToken? securityToken)
        => securityToken is JsonWebToken jsonWebToken && jsonWebToken.IssuedAt != DateTime.MinValue
            ? new DateTimeOffset(DateTime.SpecifyKind(jsonWebToken.IssuedAt, DateTimeKind.Utc))
            : null;

    [LoggerMessage(EventId = DemoLogEventIds.SandboxRevokedUserRejected, Level = LogLevel.Warning, Message = "Access token rejected because user ID '{UserId}' was deactivated, deleted or given other roles in demo sandbox {SandboxId}.")]
    private static partial void LogRevokedUserRejected(ILogger logger, Guid userId, string sandboxId);
}
