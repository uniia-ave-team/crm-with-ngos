using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides SQL-projected repository operations for managing <see cref="TaskAttachment"/> entities.
/// </summary>
/// <param name="appDbContext">The application database context.</param>
public sealed class TaskAttachmentRepository(ApplicationDbContext appDbContext)
    : GenericRepository<TaskAttachment>(appDbContext),
    ITaskAttachmentRepository
{
    /// <inheritdoc />
    public async Task<List<TResult>> GetByTaskIdAsync<TResult>(Guid taskId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(a => a.TaskId == taskId)
            .OrderByDescending(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .ProjectToType<TResult>()
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<TaskAttachment> GetByTaskAndIdAsync(Guid taskId, Guid attachmentId, CancellationToken cancellationToken = default)
        => GetAsync(a => a.TaskId == taskId && a.Id == attachmentId, attachmentId, cancellationToken);

    /// <inheritdoc />
    public async Task<TaskAttachmentStats> GetTaskAttachmentStatsAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(a => a.TaskId == taskId)
            .GroupBy(_ => 1)
            .OrderBy(g => g.Key)
            .Select(g => new TaskAttachmentStats(g.Count(), g.Sum(a => a.FileSize)))
            .FirstOrDefaultAsync(cancellationToken)
            ?? TaskAttachmentStats.Empty;
    }
}
