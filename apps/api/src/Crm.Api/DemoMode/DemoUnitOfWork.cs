using Crm.Application.Interfaces;

namespace Crm.Api.DemoMode;

/// <summary>
/// Decorator around <see cref="IUnitOfWork"/> that rolls back the active database transaction
/// instead of committing when executing inside a Demo Mode dry-run or in-memory session-auth scope,
/// and blocks unguarded <see cref="SaveChangesAsync"/> calls during HTTP requests.
/// </summary>
/// <param name="inner">The underlying <see cref="IUnitOfWork"/> implementation.</param>
/// <param name="executionContext">The Demo Mode execution scope tracker.</param>
/// <param name="logger">The logger used to record dry-run transaction rollbacks.</param>
/// <param name="demoModeState">Optional Demo Mode state service for fail-safe <see cref="SaveChangesAsync"/> guarding.</param>
/// <param name="httpContextAccessor">Optional HTTP context accessor used to distinguish HTTP requests from startup tasks.</param>
public sealed partial class DemoUnitOfWork(
    IUnitOfWork inner,
    IDemoExecutionContext executionContext,
    ILogger<DemoUnitOfWork> logger,
    IDemoModeState? demoModeState = null,
    IHttpContextAccessor? httpContextAccessor = null) : IUnitOfWork
{
    /// <inheritdoc />
    public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        => inner.BeginTransactionAsync(cancellationToken);

    /// <inheritdoc />
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (executionContext.IsDryRunActive || executionContext.IsSessionAuthActive)
        {
            await inner.RollbackTransactionAsync(CancellationToken.None);
            LogTransactionDryRunRolledBack(logger);
            return;
        }

        await inner.CommitTransactionAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        => inner.RollbackTransactionAsync(cancellationToken);

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => demoModeState?.IsActive == true &&
            httpContextAccessor?.HttpContext is not null &&
            !executionContext.IsDryRunActive &&
            !executionContext.IsExplicitlyAllowed
            ? throw new InvalidOperationException(
                "Demo Mode blocked an unguarded UnitOfWork.SaveChangesAsync call outside a dry-run transaction.")
            : inner.SaveChangesAsync(cancellationToken);

    [LoggerMessage(EventId = DemoLogEventIds.TransactionDryRunRolledBack, Level = LogLevel.Information, Message = "Demo mode dry-run completed. Transaction rolled back instead of committing.")]
    private static partial void LogTransactionDryRunRolledBack(ILogger logger);
}
