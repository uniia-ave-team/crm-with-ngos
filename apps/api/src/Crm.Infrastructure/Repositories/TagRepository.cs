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
/// Provides SQL-projected repository operations for managing project-scoped <see cref="Tag"/> entities.
/// </summary>
/// <param name="appDbContext">The application database context.</param>
public sealed class TagRepository(ApplicationDbContext appDbContext)
    : GenericRepository<Tag>(appDbContext),
    ITagRepository
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
        IQueryable<Tag> query = DbSet
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(QueryFilterExpressions.ContainsIgnoreCase<Tag>(t => t.Title, searchTerm));
        }

        query = string.IsNullOrWhiteSpace(orderBy)
            ? query.OrderBy(t => t.Id)
            : ApplyOrdering<TResult>(query, orderBy, sortOrder);

        return ToPagedResultAsync<TResult>(query, pageNumber, pageSize, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Tag> GetByProjectAndIdForUpdateAsync(Guid projectId, Guid tagId, CancellationToken cancellationToken = default)
        => GetForUpdateAsync(t => t.ProjectId == projectId && t.Id == tagId, tagId, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> ExistsWithTitleAsync(Guid projectId, string title, Guid? excludeTagId = null, CancellationToken cancellationToken = default)
    {
        string normalizedTitle = title.NormalizeTitle();

        return await DbSet
            .AsNoTracking()
            .AnyAsync(
                t => t.ProjectId == projectId
                     && (excludeTagId == null || t.Id != excludeTagId.Value)
                     && t.NormalizedTitle == normalizedTitle,
                cancellationToken);
    }

    /// <inheritdoc />
    public Task EnsureExistsInProjectAsync(Guid projectId, Guid tagId, CancellationToken cancellationToken = default)
        => EnsureExistsAsync(t => t.ProjectId == projectId && t.Id == tagId, tagId, cancellationToken);

    /// <inheritdoc />
    public Task EnsureAllExistInProjectAsync(Guid projectId, IEnumerable<Guid> tagIds, CancellationToken cancellationToken = default)
        => EnsureAllExistAsync(tagIds, t => t.ProjectId == projectId, cancellationToken);

    /// <inheritdoc />
    public Task<bool> AddToTaskAsync(Guid taskId, Guid tagId, CancellationToken cancellationToken = default)
        => AddJoinIfNotExistsAsync(
            x => x.TaskId == taskId && x.TagId == tagId,
            new TaskTag { TaskId = taskId, TagId = tagId },
            cancellationToken);

    /// <inheritdoc />
    public Task AddToTaskAsync(Guid taskId, IEnumerable<Guid> tagIds, CancellationToken cancellationToken = default)
        => AddJoinRangeAsync(
            tagIds,
            tagId => new TaskTag { TaskId = taskId, TagId = tagId },
            cancellationToken);

    /// <inheritdoc />
    public Task SyncTaskTagsAsync(Guid taskId, IEnumerable<Guid> tagIds, CancellationToken cancellationToken = default)
        => SyncJoinAsync(
            x => x.TaskId == taskId,
            x => x.TagId,
            tagIds,
            tagId => new TaskTag { TaskId = taskId, TagId = tagId },
            cancellationToken);

    /// <inheritdoc />
    public Task<bool> RemoveFromTaskAsync(Guid taskId, Guid tagId, CancellationToken cancellationToken = default)
        => RemoveJoinAsync<TaskTag>(x => x.TaskId == taskId && x.TagId == tagId, cancellationToken);
}
