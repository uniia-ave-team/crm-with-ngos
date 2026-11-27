using System.Linq.Expressions;
using Crm.Application.Dtos.Tasks;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Expressions;
using Crm.Infrastructure.Persistence.Extensions;
using Crm.Infrastructure.Persistence.Sorting;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides SQL-projected repository operations for managing <see cref="ProjectTask"/> entities
/// and strongly-typed dynamic sorting.
/// </summary>
/// <param name="appDbContext">The application database context.</param>
public class ProjectTaskRepository(ApplicationDbContext appDbContext)
    : GenericRepository<ProjectTask>(appDbContext),
    IProjectTaskRepository
{
    /// <inheritdoc />
    public async Task<TResult> GetByProjectAndIdAsync<TResult>(Guid projectId, Guid taskId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId && t.Id == taskId)
            .ProjectToType<TResult>()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException<ProjectTask>(taskId);
    }

    /// <inheritdoc />
    public Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        ProjectTaskFilter filter,
        Guid currentUserId,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        IQueryable<ProjectTask> query = ApplyTaskFilters(DbSet.AsNoTracking(), filter, currentUserId);
        query = ApplyTaskOrdering<TResult>(query, currentUserId, filter.IsMine, orderBy, sortOrder);

        return ToPagedResultAsync<TResult>(query, pageNumber, pageSize, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Dictionary<Guid, int>> GetUnreadCommentCountsAsync(
        IEnumerable<Guid> taskIds,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        List<Guid> ids = [.. taskIds.Distinct()];

        return ids.Count == 0
            ? []
            : await Context.Set<TaskReadState>()
                .AsNoTracking()
                .Where(rs => rs.UserId == userId && ids.Contains(rs.Comment.TaskId))
                .GroupBy(rs => rs.Comment.TaskId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ProjectTask> GetByProjectAndIdForUpdateAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default)
        => GetForUpdateAsync(t => t.ProjectId == projectId && t.Id == taskId, taskId, cancellationToken);

    /// <inheritdoc />
    public Task EnsureExistsInProjectAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default)
        => EnsureExistsAsync(t => t.ProjectId == projectId && t.Id == taskId, taskId, cancellationToken);

    /// <inheritdoc />
    public Task UpdateUpdatedAtAsync(Guid taskId, DateTime updatedAt, CancellationToken cancellationToken = default)
        => DbSet
            .Where(t => t.Id == taskId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UpdatedAt, updatedAt), cancellationToken);

    /// <inheritdoc />
    public async Task RemoveInaccessibleUserDataAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await Context.Set<TaskUser>()
            .Where(tu => tu.Task.ProjectId == projectId
                         && !Context.Set<ProjectMember>().Any(m => m.ProjectId == projectId && m.UserId == tu.UserId))
            .ExecuteDeleteAsync(cancellationToken);

        await Context.Set<TaskReadState>()
            .Where(rs => rs.Comment.Task.ProjectId == projectId
                         && !Context.Set<ProjectMember>().Any(m => m.ProjectId == projectId && m.UserId == rs.UserId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> AssignToTaskAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default)
        => AddJoinIfNotExistsAsync(
            x => x.TaskId == taskId && x.UserId == userId,
            new TaskUser { TaskId = taskId, UserId = userId },
            cancellationToken);

    /// <inheritdoc />
    public Task AssignToTaskAsync(Guid taskId, IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
        => AddJoinRangeAsync(
            userIds,
            userId => new TaskUser { TaskId = taskId, UserId = userId },
            cancellationToken);

    /// <inheritdoc />
    public Task SyncTaskUsersAsync(Guid taskId, IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
        => SyncJoinAsync(
            x => x.TaskId == taskId,
            x => x.UserId,
            userIds,
            userId => new TaskUser { TaskId = taskId, UserId = userId },
            cancellationToken);

    /// <inheritdoc />
    public Task<bool> UnassignFromTaskAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default)
        => RemoveJoinAsync<TaskUser>(x => x.TaskId == taskId && x.UserId == userId, cancellationToken);

    /// <summary>
    /// Applies filter criteria from <paramref name="filter"/> to the task queryable.
    /// When <see cref="ProjectTaskFilter.ProjectId"/> is <c>null</c>, restricts results to tasks whose parent
    /// project is visible to <paramref name="currentUserId"/>.
    /// </summary>
    /// <param name="query">The source task queryable.</param>
    /// <param name="filter">The structured task filter criteria.</param>
    /// <param name="currentUserId">The unique identifier of the current user.</param>
    /// <returns>The filtered task queryable.</returns>
    protected static IQueryable<ProjectTask> ApplyTaskFilters(
        IQueryable<ProjectTask> query,
        ProjectTaskFilter filter,
        Guid currentUserId)
    {
        ArgumentNullException.ThrowIfNull(filter);

        query = filter.ProjectId is { } projectId
            ? query.Where(t => t.ProjectId == projectId)
            : query.Where(ProjectVisibilityExpressions.HasVisibleProject<ProjectTask>(t => t.Project, currentUserId));

        query = query
            .WhereIf(filter.IsMine, t => t.AssignedUsers.Any(u => u.Id == currentUserId))
            .WhereIf(filter.TopicId.HasValue, () =>
            {
                Guid topicId = filter.TopicId!.Value;
                return t => t.TopicId == topicId;
            })
            .WhereIf(filter.Status.HasValue, () =>
            {
                var status = filter.Status!.Value;
                return t => t.Status == status;
            })
            .WhereIf(filter.Priority.HasValue, () =>
            {
                var priority = filter.Priority!.Value;
                return t => t.Priority == priority;
            })
            .WhereIf(filter.TagIds is { Count: > 0 }, () =>
            {
                List<Guid> tagFilterList = [.. filter.TagIds!.Distinct()];
                return t => t.Tags.Any(tag => tagFilterList.Contains(tag.Id));
            })
            .WhereIf(filter.StartDateFrom.HasValue, () =>
            {
                DateTime startFrom = filter.StartDateFrom!.Value;
                return t => t.StartDate >= startFrom;
            })
            .WhereIf(filter.StartDateTo.HasValue, () =>
            {
                DateTime startTo = filter.StartDateTo!.Value;
                return t => t.StartDate <= startTo;
            })
            .WhereIf(filter.EndDateFrom.HasValue, () =>
            {
                DateTime endFrom = filter.EndDateFrom!.Value;
                return t => t.EndDate.HasValue && t.EndDate.Value >= endFrom;
            })
            .WhereIf(filter.EndDateTo.HasValue, () =>
            {
                DateTime endTo = filter.EndDateTo!.Value;
                return t => t.EndDate.HasValue && t.EndDate.Value <= endTo;
            })
            .WhereIf(
                !string.IsNullOrWhiteSpace(filter.SearchTerm),
                () => QueryFilterExpressions.ContainsIgnoreCase<ProjectTask>(
                    filter.SearchTerm!,
                    t => t.Title,
                    t => t.Description));

        return query;
    }

    /// <summary>
    /// Applies task ordering with null-safe <see cref="TaskSummaryDto.EndDate"/> sorting
    /// and native <see cref="GenericRepository{T}.ApplyOrdering{TResult}"/> fallback.
    /// </summary>
    /// <typeparam name="TResult">The target DTO or projection type.</typeparam>
    /// <param name="query">The source task queryable.</param>
    /// <param name="orderBy">The requested DTO sort property name.</param>
    /// <param name="sortOrder">The sort direction (<c>asc</c> or <c>desc</c>).</param>
    /// <returns>The ordered task queryable.</returns>
    protected override IQueryable<ProjectTask> ApplyOrdering<TResult>(
        IQueryable<ProjectTask> query,
        string orderBy,
        string? sortOrder)
    {
        string normalized = orderBy.Trim();
        return string.Equals(normalized, nameof(TaskSummaryDto.EndDate), StringComparison.OrdinalIgnoreCase)
            ? query.ApplyNullsLastOrdering(t => t.EndDate, t => t.Id, sortOrder.IsDescending(), nullsLastInAscending: true)
            : base.ApplyOrdering<TResult>(query, normalized, sortOrder);
    }

    /// <summary>
    /// Applies default personal (<c>filter=mine</c>) or board ordering, <see cref="TaskSummaryDto.UnreadCommentsCount"/>
    /// and <see cref="TaskSummaryDto.AttachmentsCount"/> sorting, or delegates DTO property sorting to <see cref="ApplyOrdering{TResult}"/>.
    /// </summary>
    /// <typeparam name="TResult">The target DTO or projection type.</typeparam>
    /// <param name="query">The source task queryable.</param>
    /// <param name="currentUserId">The unique identifier of the current user.</param>
    /// <param name="isMine">Indicates whether personal default sorting should be used when <paramref name="orderBy"/> is omitted.</param>
    /// <param name="orderBy">An optional DTO property name to sort by.</param>
    /// <param name="sortOrder">The sort direction (<c>asc</c> or <c>desc</c>).</param>
    /// <returns>The ordered task queryable.</returns>
    protected IQueryable<ProjectTask> ApplyTaskOrdering<TResult>(
        IQueryable<ProjectTask> query,
        Guid currentUserId,
        bool isMine = false,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending)
    {
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return isMine
                ? query
                    .OrderByDescending(UnreadCountExpression(currentUserId))
                    .ThenBy(t => t.EndDate ?? t.StartDate)
                    .ThenByDescending(t => t.CreatedAt)
                    .ThenBy(t => t.Id)
                : query
                    .OrderBy(t => t.StartDate)
                    .ThenByDescending(t => t.CreatedAt)
                    .ThenBy(t => t.Id);
        }

        string normalized = orderBy.Trim();
        bool descending = sortOrder.IsDescending();

        return normalized switch
        {
            var f when f.Equals(nameof(TaskSummaryDto.UnreadCommentsCount), StringComparison.OrdinalIgnoreCase) => descending
                ? query.OrderByDescending(UnreadCountExpression(currentUserId)).ThenBy(t => t.Id)
                : query.OrderBy(UnreadCountExpression(currentUserId)).ThenBy(t => t.Id),

            var f when f.Equals(nameof(TaskSummaryDto.AttachmentsCount), StringComparison.OrdinalIgnoreCase) => descending
                ? query.OrderByDescending(t => t.Attachments.Count).ThenBy(t => t.Id)
                : query.OrderBy(t => t.Attachments.Count).ThenBy(t => t.Id),

            _ => ApplyOrdering<TResult>(query, normalized, sortOrder),
        };
    }

    private static Expression<Func<ProjectTask, int>> UnreadCountExpression(Guid currentUserId)
        => t => t.Comments.Count(c => c.ReadStates.Any(rs => rs.UserId == currentUserId));
}
