using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides SQL-projected repository operations for managing <see cref="Topic"/> entities.
/// </summary>
/// <param name="appDbContext">The application database context.</param>
public sealed class TopicRepository(ApplicationDbContext appDbContext)
    : GenericRepository<Topic>(appDbContext),
    ITopicRepository
{
    /// <inheritdoc />
    public Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        Guid projectId,
        string? searchTerm = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Topic> query = DbSet
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(QueryFilterExpressions.ContainsIgnoreCase<Topic>(t => t.Title, searchTerm));
        }

        query = string.IsNullOrWhiteSpace(orderBy)
            ? query.OrderBy(t => t.Order).ThenBy(t => t.Id)
            : ApplyOrdering<TResult>(query, orderBy, sortOrder);

        return ToPagedResultAsync<TResult>(query, pageNumber, pageSize, cancellationToken);
    }

    /// <inheritdoc />
    public Task<List<Topic>> GetByProjectIdForUpdateAsync(Guid projectId, CancellationToken cancellationToken = default)
        => DbSet
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.Order)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<List<byte>> GetOrdersByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default)
        => DbSet
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .Select(t => t.Order)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task ResolveOrderCollisionsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        Context.ChangeTracker.DetectChanges();

        var trackedEntries = Context.ChangeTracker.Entries<Topic>()
            .Where(e => e.Entity.ProjectId == projectId && e.State != EntityState.Detached)
            .ToList();

        var modifiedOrderEntries = trackedEntries
            .Where(e =>
                e.State == EntityState.Modified
                && e.Property(nameof(Topic.Order)).OriginalValue is byte oldOrder
                && oldOrder != e.Entity.Order)
            .ToList();

        if (modifiedOrderEntries.Count == 0)
        {
            return;
        }

        var targetOrders = modifiedOrderEntries
            .Select(e => e.Entity.Order)
            .ToHashSet();

        var collidingEntries = modifiedOrderEntries
            .Where(e =>
                e.Property(nameof(Topic.Order)).OriginalValue is byte oldOrder
                && targetOrders.Contains(oldOrder))
            .ToList();

        if (collidingEntries.Count == 0)
        {
            return;
        }

        var reservedOrders = new HashSet<byte>();
        foreach (var entry in trackedEntries)
        {
            reservedOrders.Add(entry.Entity.Order);
            if (entry.Property(nameof(Topic.Order)).OriginalValue is byte originalOrder)
            {
                reservedOrders.Add(originalOrder);
            }
        }

        int candidate = byte.MaxValue;
        foreach (var entry in collidingEntries)
        {
            while (candidate >= 0 && reservedOrders.Contains((byte)candidate))
            {
                candidate--;
            }

            if (candidate < 0)
            {
                throw new InvalidOperationException("No temporary display order slot is available to resolve topic order collisions.");
            }

            byte tempOrder = (byte)candidate;
            reservedOrders.Add(tempOrder);

            await DbSet
                .Where(t => t.Id == entry.Entity.Id)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(t => t.Order, tempOrder),
                    cancellationToken);

            entry.Property(nameof(Topic.Order)).OriginalValue = tempOrder;
        }
    }

    /// <inheritdoc />
    public Task<Topic> GetByProjectAndIdForUpdateAsync(Guid projectId, Guid topicId, CancellationToken cancellationToken = default)
        => GetForUpdateAsync(t => t.ProjectId == projectId && t.Id == topicId, topicId, cancellationToken);

    /// <inheritdoc />
    public Task EnsureExistsInProjectAsync(Guid projectId, Guid topicId, CancellationToken cancellationToken = default)
        => EnsureExistsAsync(t => t.ProjectId == projectId && t.Id == topicId, topicId, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> ExistsWithTitleAsync(
        Guid projectId,
        string title,
        Guid? excludeTopicId = null,
        CancellationToken cancellationToken = default)
    {
        string normalizedTitle = title.NormalizeTitle();

        return await DbSet
            .AsNoTracking()
            .AnyAsync(
                t => t.ProjectId == projectId
                     && (excludeTopicId == null || t.Id != excludeTopicId.Value)
                     && t.NormalizedTitle == normalizedTitle,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsWithOrderAsync(
        Guid projectId,
        byte order,
        Guid? excludeTopicId = null,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .AnyAsync(
                t => t.ProjectId == projectId
                     && (excludeTopicId == null || t.Id != excludeTopicId.Value)
                     && t.Order == order,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> HasTasksAsync(Guid topicId, CancellationToken cancellationToken = default)
    {
        return await Context.Set<ProjectTask>()
            .AsNoTracking()
            .AnyAsync(t => t.TopicId == topicId, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Soft-deleted tasks still reference the topic, and ProjectTask -> Topic uses Restrict delete
        // behavior: without purging them first, a topic that ever had a task deleted could never be
        // deleted. Live tasks are left alone (callers refuse to delete a topic that has any, see
        // HasTasksAsync); the rows of the purged tasks' comments, attachments, assignees and tags cascade.
        await Context.Set<ProjectTask>()
            .IgnoreQueryFilters()
            .Where(t => t.TopicId == id && t.IsDeleted)
            .ExecuteDeleteAsync(cancellationToken);

        await base.DeleteAsync(id, cancellationToken);
    }
}
