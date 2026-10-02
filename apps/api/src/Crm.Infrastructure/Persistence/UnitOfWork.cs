using Crm.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace Crm.Infrastructure.Persistence;

/// <summary>
/// Represents the Unit of Work pattern implementation using Entity Framework Core,
/// managing database transactions and persistence boundaries.
/// </summary>
/// <param name="context">The application database context instance.</param>
public class UnitOfWork(ApplicationDbContext context) : IUnitOfWork, IAsyncDisposable
{
    private IDbContextTransaction? _currentTransaction;

    /// <summary>
    /// Begins a new database transaction asynchronously if a transaction is not already active.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            return;
        }

        _currentTransaction = await context.Database.BeginTransactionAsync(cancellationToken);
    }

    /// <summary>
    /// Saves all pending changes to the database and commits the active transaction,
    /// ensuring proper resource cleanup afterward.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);

            if (_currentTransaction != null)
            {
                await _currentTransaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
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
            if (_currentTransaction != null)
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

    public async ValueTask DisposeAsync()
    {
        await DisposeTransactionAsync();
        GC.SuppressFinalize(this);
    }

    private async Task DisposeTransactionAsync()
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }
}
