namespace Crm.Infrastructure.Auditing;

/// <summary>
/// Provides safe, non-throwing access to the current actor identity and correlation trace identifier
/// for entity audit logging across HTTP requests, CLI commands, and background workers.
/// </summary>
public interface IAuditActorAccessor
{
    /// <summary>
    /// Attempts to resolve the unique identifier of the currently authenticated user,
    /// returning <c>null</c> when running outside an authenticated HTTP request.
    /// </summary>
    /// <returns>The actor's user identifier, or <c>null</c> for system/anonymous operations.</returns>
    Guid? GetActorUserId();

    /// <summary>
    /// Resolves the current distributed trace or HTTP request correlation identifier, if available.
    /// </summary>
    /// <returns>The correlation trace identifier, or <c>null</c>.</returns>
    string? GetTraceId();
}
