namespace Crm.Api.DemoMode;

/// <summary>
/// Tracks the active Demo Mode execution policy for the current async execution flow.
/// Uses <see cref="AsyncLocal{T}"/> so both scoped and singleton decorators observe the exact command scope.
/// </summary>
public interface IDemoExecutionContext
{
    /// <summary>
    /// Gets the current <see cref="DemoExecutionMode"/> for the active async execution flow.
    /// </summary>
    DemoExecutionMode Mode { get; }

    /// <summary>
    /// Gets a value indicating whether the active async execution flow should roll back mutations instead of committing.
    /// </summary>
    bool IsDryRunActive { get; }

    /// <summary>
    /// Gets a value indicating whether the active async execution flow is a Demo Mode authentication session command
    /// (persisting tokens in memory while keeping the database read-only).
    /// </summary>
    bool IsSessionAuthActive { get; }

    /// <summary>
    /// Gets a value indicating whether the active async execution flow is explicitly allowed to commit to the database.
    /// </summary>
    bool IsExplicitlyAllowed { get; }

    /// <summary>
    /// Enters a Demo Mode execution scope with the specified <paramref name="mode"/>
    /// and restores the prior value on disposal.
    /// </summary>
    /// <param name="mode">The execution mode active within the scope.</param>
    /// <returns>A disposable token restoring the previous scope state.</returns>
    IDisposable EnterScope(DemoExecutionMode mode);

    /// <summary>
    /// Enters a dry-run or explicitly-allowed scope for the current async execution flow
    /// and restores the prior value on disposal.
    /// </summary>
    /// <param name="shouldDryRun">Whether dry-run rollback should be active within the scope.</param>
    /// <returns>A disposable token restoring the previous scope state.</returns>
    IDisposable EnterScope(bool shouldDryRun);
}
