namespace Crm.Api.DemoMode;

/// <summary>
/// Defines structured logging event identifiers isolated for Demo Mode diagnostics.
/// </summary>
internal static class DemoLogEventIds
{
    public const int DemoModeWriteBlocked = 99002;
    public const int UnguardedDbWriteBlocked = 99003;
    public const int SandboxCreated = 99010;
    public const int SandboxRemoved = 99011;
    public const int SandboxCapacityReached = 99012;
    public const int SandboxCreationRateLimited = 99013;
    public const int SandboxSessionExpired = 99014;
    public const int SandboxTemplatePrepared = 99015;
    public const int SandboxLeftoverDropped = 99016;
    public const int SandboxCleanupFailed = 99017;
    public const int SandboxRemovalFailed = 99018;
    public const int SandboxTemplateRetry = 99019;
    public const int SandboxUploadQuotaExceeded = 99020;
    public const int DemoDataSeeded = 99021;
}
