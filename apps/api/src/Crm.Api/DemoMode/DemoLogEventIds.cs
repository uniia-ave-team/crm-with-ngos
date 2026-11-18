namespace Crm.Api.DemoMode;

/// <summary>
/// Defines structured logging event identifiers isolated for Demo Mode diagnostics.
/// </summary>
internal static class DemoLogEventIds
{
    public const int TransactionDryRunRolledBack = 99001;
    public const int DemoModeWriteBlocked = 99002;
    public const int UnguardedDbWriteBlocked = 99003;
    public const int DemoCommandIntercepted = 99004;
}
