namespace Crm.Infrastructure.Persistence.Consts;

/// <summary>
/// Provides centralized physical database table name constants used by EF Core entity configurations.
/// </summary>
internal static class TableNames
{
    public const string AuditLogs = "AuditLogs";
    public const string AuthRoleClaims = "AuthRoleClaims";
    public const string AuthRoles = "AuthRoles";
    public const string AuthUserClaims = "AuthUserClaims";
    public const string AuthUserLogins = "AuthUserLogins";
    public const string AuthUserRoles = "AuthUserRoles";
    public const string AuthUsers = "AuthUsers";
    public const string AuthUserTokens = "AuthUserTokens";
    public const string LoginPageImages = "LoginPageImages";
    public const string MaintenanceNotice = nameof(Domain.Entities.MaintenanceNotice);
    public const string ProjectTask = "Task";
    public const string TaskComment = "Comment";
    public const string TaskReadState = "TaskReadState";
    public const string TaskTag = "TaskTag";
    public const string TaskUser = "TaskUser";
    public const string UserRefreshTokens = "UserRefreshTokens";
}
