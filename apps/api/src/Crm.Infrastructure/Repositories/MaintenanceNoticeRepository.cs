using Crm.Domain.Entities;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Auditing;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides Entity Framework persistence for the singleton <see cref="MaintenanceNotice"/> record.
/// Staged changes are persisted by the caller via <see cref="Application.Interfaces.IUnitOfWork.SaveChangesAsync"/>.
/// </summary>
/// <param name="context">The application database context.</param>
public sealed class MaintenanceNoticeRepository(ApplicationDbContext context) : IMaintenanceNoticeRepository
{
    private DbSet<MaintenanceNotice> DbSet { get; } = context.Set<MaintenanceNotice>();

    /// <inheritdoc />
    public Task<MaintenanceNotice?> GetAsync(CancellationToken cancellationToken = default)
        => DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == MaintenanceNotice.SingletonId, cancellationToken);

    /// <inheritdoc />
    public async Task UpsertAsync(
        string message,
        DateTime startsAt,
        DateTime endsAt,
        DateTime updatedAt,
        CancellationToken cancellationToken = default)
    {
        var existing = await DbSet.FirstOrDefaultAsync(
            n => n.Id == MaintenanceNotice.SingletonId,
            cancellationToken);

        if (existing is null)
        {
            await DbSet.AddAsync(
                new MaintenanceNotice().Update(message, startsAt, endsAt, updatedAt),
                cancellationToken);
        }
        else
        {
            existing.Update(message, startsAt, endsAt, updatedAt);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ClearAsync(CancellationToken cancellationToken = default)
    {
        int rowsAffected = await DbSet
            .Where(n => n.Id == MaintenanceNotice.SingletonId)
            .ExecuteDeleteAsync(cancellationToken);

        if (rowsAffected > 0)
        {
            context.TryEnqueueBulkDeleteAudit<MaintenanceNotice>(MaintenanceNotice.SingletonId.ToString());
        }

        return rowsAffected > 0;
    }
}
