using System.Security.Claims;
using Crm.Domain.Constants;
using Crm.Infrastructure.Services.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Crm.Api.DemoMode.Sandbox;

/// <summary>
/// Runs the regular <see cref="JwtBearerEventsHandler"/> and then rejects the access tokens of users who were
/// deactivated or deleted inside the token's own sandbox (see <see cref="DemoSandboxRevokedUsersCache"/>).
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
            && sandbox.IsUserRevoked(userId))
        {
            LogRevokedUserRejected(logger, userId, sandbox.Id);
            context.Fail("User access has been revoked.");
        }
    }

    [LoggerMessage(EventId = DemoLogEventIds.SandboxRevokedUserRejected, Level = LogLevel.Warning, Message = "Access token rejected because user ID '{UserId}' was deactivated or deleted in demo sandbox {SandboxId}.")]
    private static partial void LogRevokedUserRejected(ILogger logger, Guid userId, string sandboxId);
}
