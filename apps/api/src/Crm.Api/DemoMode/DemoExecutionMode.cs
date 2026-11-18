namespace Crm.Api.DemoMode;

/// <summary>
/// Represents the active execution policy within a Demo Mode request scope.
/// </summary>
public enum DemoExecutionMode
{
    /// <summary>
    /// No Demo Mode command scope is currently active.
    /// </summary>
    None = 0,

    /// <summary>
    /// Mutating database operations run inside a transaction that is rolled back before commit,
    /// and file/cache mutations are suppressed.
    /// </summary>
    DryRun = 1,

    /// <summary>
    /// Authentication session commands (<c>login</c>, <c>refresh-token</c>, <c>logout</c>) that store
    /// refresh tokens in the bounded in-memory Demo Mode token store without writing to the database.
    /// </summary>
    SessionAuth = 2,

    /// <summary>
    /// Explicitly allowed commands (<see cref="IDemoAllowed"/> or <see cref="DemoAllowAttribute"/>)
    /// permitted to commit normally even when Demo Mode is active.
    /// </summary>
    ExplicitlyAllowed = 3,
}
