using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides Entity Framework persistence for the singleton <see cref="MaintenanceNotice"/> record.
/// </summary>
public class MaintenanceNoticeRepository(ApplicationDbContext context) : IMaintenanceNoticeRepository
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
                new MaintenanceNotice
                {
                    Id = MaintenanceNotice.SingletonId,
                    Message = message,
                    StartsAt = startsAt,
                    EndsAt = endsAt,
                    UpdatedAt = updatedAt,
                },
                cancellationToken);
        }
        else
        {
            existing.Message = message;
            existing.StartsAt = startsAt;
            existing.EndsAt = endsAt;
            existing.UpdatedAt = updatedAt;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ClearAsync(CancellationToken cancellationToken = default)
    {
        int rowsAffected = await DbSet
            .Where(n => n.Id == MaintenanceNotice.SingletonId)
            .ExecuteDeleteAsync(cancellationToken);

        return rowsAffected > 0;
    }
}
