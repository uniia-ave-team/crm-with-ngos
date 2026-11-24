namespace Crm.Api.DemoMode;

/// <summary>
/// The <c>errorCode</c> values the API returns in demo mode. The web client shows a message for each of them.
/// </summary>
public static class DemoErrorCodes
{
    /// <summary>
    /// The name of the problem details extension that carries the error code.
    /// </summary>
    public const string ExtensionKey = "errorCode";

    /// <summary>
    /// 403: the action is switched off in the demo.
    /// </summary>
    public const string Restricted = "DEMO_MODE_RESTRICTED";

    /// <summary>
    /// 403: a demo limit (for example the upload quota) is reached.
    /// </summary>
    public const string QuotaExceeded = "DEMO_QUOTA_EXCEEDED";

    /// <summary>
    /// 401: the visitor's sandbox no longer exists; signing in again starts a fresh one.
    /// </summary>
    public const string SessionExpired = "DEMO_SESSION_EXPIRED";

    /// <summary>
    /// 503: too many visitors use the demo at the same time.
    /// </summary>
    public const string CapacityReached = "DEMO_CAPACITY_REACHED";

    /// <summary>
    /// 429: too many new demo sessions were started from one address.
    /// </summary>
    public const string TooManySessions = "DEMO_TOO_MANY_SESSIONS";

    /// <summary>
    /// The message for <see cref="Restricted"/>.
    /// </summary>
    public const string RestrictedMessage = "This operation is disabled in Demo Mode.";

    /// <summary>
    /// The message for <see cref="QuotaExceeded"/>.
    /// </summary>
    public const string QuotaExceededMessage = "The upload limit of the demo session is reached.";

    /// <summary>
    /// The message for <see cref="SessionExpired"/>.
    /// </summary>
    public const string SessionExpiredMessage = "The demo session has ended. Sign in again to start with fresh demo data.";

    /// <summary>
    /// The message for <see cref="CapacityReached"/>.
    /// </summary>
    public const string CapacityReachedMessage = "The demo is busy right now. Try again in a few minutes.";

    /// <summary>
    /// The message for <see cref="TooManySessions"/>.
    /// </summary>
    public const string TooManySessionsMessage = "Too many demo sessions were started from this address. Try again later.";
}
