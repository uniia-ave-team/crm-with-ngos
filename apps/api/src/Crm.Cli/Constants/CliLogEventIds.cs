namespace Crm.Cli.Constants;

/// <summary>
/// Defines standardized event identifiers for structured logging within the CLI application.
/// Event IDs are partitioned into numerical ranges to prevent collisions and simplify log filtering.
/// </summary>
public static class CliLogEventIds
{
    // 5000s: CLI Routing, Dispatching, and General Execution
    public const int CommandNotFound = 5001;

    // 5100s: Admin & Identity Operations (e.g., reset-admin-password)
    public const int PasswordArgumentMissing = 5101;
    public const int PasswordsDoNotMatch = 5102;
    public const int EmailArgumentMissing = 5103;
    public const int AdminUserNotFound = 5104;
    public const int UserRetrievalFailed = 5105;
    public const int PasswordResetFailed = 5106;
    public const int PasswordResetSuccess = 5107;
    public const int UserNotAdmin = 5108;
}
