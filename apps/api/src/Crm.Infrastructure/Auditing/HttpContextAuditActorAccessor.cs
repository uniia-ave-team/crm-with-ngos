using System.Diagnostics;
using Crm.Infrastructure.Extensions;
using Microsoft.AspNetCore.Http;

namespace Crm.Infrastructure.Auditing;

/// <summary>
/// Default <see cref="IAuditActorAccessor"/> that safely resolves the authenticated user identifier
/// and correlation trace identifier from <see cref="IHttpContextAccessor"/> and <see cref="Activity.Current"/>.
/// Never throws outside of an HTTP request (for example, in background services or CLI commands).
/// </summary>
/// <param name="httpContextAccessor">Optional HTTP context accessor for active web requests.</param>
public sealed class HttpContextAuditActorAccessor(IHttpContextAccessor? httpContextAccessor = null)
    : IAuditActorAccessor
{
    /// <inheritdoc />
    public Guid? GetActorUserId()
        => httpContextAccessor?.HttpContext?.User.TryGetUserId(out Guid userId) == true
            ? userId
            : null;

    /// <inheritdoc />
    public string? GetTraceId()
        => Activity.Current?.Id ?? httpContextAccessor?.HttpContext?.TraceIdentifier;
}
