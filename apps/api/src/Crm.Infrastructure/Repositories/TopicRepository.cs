using Crm.Domain.Common;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Helpers;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides SQL-projected repository operations for managing <see cref="Topic"/> entities.
/// </summary>
/// <param name="appDbContext">The application database context.</param>
public class TopicRepository(ApplicationDbContext appDbContext)
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
            ? query.OrderBy(t => t.Id)
            : ApplyOrdering<TResult>(query, orderBy, sortOrder);

        return ToPagedResultAsync<TResult>(query, pageNumber, pageSize, cancellationToken);
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
}
