using Crm.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace Crm.Infrastructure.Persistence;

/// <summary>
/// Represents the Unit of Work pattern implementation using Entity Framework Core,
/// managing database transactions and persistence boundaries.
/// </summary>
/// <param name="context">The application database context instance.</param>
public sealed class UnitOfWork(ApplicationDbContext context) : IUnitOfWork, IDisposable, IAsyncDisposable
{
    private IDbContextTransaction? _currentTransaction;
    private int _transactionDepth;

    /// <summary>
    /// Begins a new database transaction asynchronously if a transaction is not already active,
    /// or participates in the active transaction by tracking nesting depth.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is not null)
        {
            _transactionDepth++;
            return;
        }

        _currentTransaction = await context.Database.BeginTransactionAsync(cancellationToken);
        _transactionDepth = 1;
    }

    /// <summary>
    /// Saves all pending changes to the database and commits the active transaction when the outermost scope completes,
    /// ensuring proper resource cleanup afterward without masking the original exception on rollback failure.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transactionDepth > 1)
        {
            await context.SaveChangesAsync(cancellationToken);
            _transactionDepth--;
            return;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);

            if (_currentTransaction is not null)
            {
                await _currentTransaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            try
            {
                await RollbackTransactionAsync(CancellationToken.None);
            }
            catch
            {
                // Preserve the original failure that triggered the rollback.
            }

            throw;
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    /// <summary>
    /// Rolls back the active database transaction, discarding any uncommitted changes,
    /// and ensures proper resource cleanup afterward.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_currentTransaction is not null)
            {
                await _currentTransaction.RollbackAsync(cancellationToken);
            }
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    /// <summary>
    /// Asynchronously saves all pending changes tracked by the database context to the database
    /// without committing or closing the active transaction.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation, containing the number of state entries written to the database.</returns>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        _currentTransaction?.Dispose();
        _currentTransaction = null;
        _transactionDepth = 0;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeTransactionAsync();
    }

    private async Task DisposeTransactionAsync()
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }

        _transactionDepth = 0;
    }
}
