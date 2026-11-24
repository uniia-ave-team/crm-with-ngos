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

    // 5200s: First Admin Creation (create-admin)
    public const int CreateAdminMissingArguments = 5201;
    public const int CreateAdminValidationFailed = 5202;
    public const int CreateAdminUsersAlreadyExist = 5203;
    public const int CreateAdminOperationFailed = 5204;
    public const int CreateAdminUnexpectedError = 5205;
    public const int CreateAdminSuccess = 5206;

    // 5300s: Scheduled Maintenance Notice (notice)
    public const int NoticeInvalidSubcommand = 5301;
    public const int NoticeValidationFailed = 5302;
    public const int NoticeEndTimeInPast = 5303;
    public const int NoticeSetSuccess = 5304;
    public const int NoticeCleared = 5305;
    public const int NoticeAlreadyClear = 5306;
    public const int NoticeUnexpectedError = 5307;
}
