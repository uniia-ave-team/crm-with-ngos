using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides SQL-projected repository operations for managing <see cref="TaskComment"/> entities
/// and <see cref="TaskReadState"/> unread markers.
/// </summary>
/// <param name="appDbContext">The application database context.</param>
public sealed class TaskCommentRepository(ApplicationDbContext appDbContext)
    : GenericRepository<TaskComment>(appDbContext),
    ITaskCommentRepository
{
    /// <inheritdoc />
    public Task<PagedResult<TResult>> GetPagedByTaskIdAsync<TResult>(
        Guid taskId,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TaskComment> query = DbSet
            .AsNoTracking()
            .Where(c => c.TaskId == taskId)
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id);

        return ToPagedResultAsync<TResult>(query, pageNumber, pageSize, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TaskComment> GetByTaskAndIdForUpdateAsync(Guid taskId, Guid commentId, CancellationToken cancellationToken = default)
        => GetForUpdateAsync(c => c.TaskId == taskId && c.Id == commentId, commentId, cancellationToken);

    /// <inheritdoc />
    public async Task MarkUnreadForOtherUsersAsync(
        TaskComment comment,
        Guid authorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comment);

        var taskInfo = await Context.Set<ProjectTask>()
            .AsNoTracking()
            .Where(t => t.Id == comment.TaskId)
            .Select(t => new { t.ProjectId, t.Project.Visibility })
            .FirstOrDefaultAsync(cancellationToken);

        if (taskInfo is null)
        {
            return;
        }

        IQueryable<Guid> memberIds = Context.Set<ProjectMember>()
            .AsNoTracking()
            .Where(m => m.ProjectId == taskInfo.ProjectId && m.UserId != authorUserId && m.User.IsActive)
            .Select(m => m.UserId);

        IQueryable<Guid> recipientQuery;
        if (taskInfo.Visibility == ProjectVisibility.Private)
        {
            recipientQuery = memberIds.Distinct();
        }
        else
        {
            IQueryable<Guid> assigneeIds = Context.Set<TaskUser>()
                .AsNoTracking()
                .Where(tu => tu.TaskId == comment.TaskId && tu.UserId != authorUserId && tu.User.IsActive)
                .Select(tu => tu.UserId);

            IQueryable<Guid> commenterIds = DbSet
                .AsNoTracking()
                .Where(c => c.TaskId == comment.TaskId
                            && c.UserId.HasValue
                            && c.UserId.Value != authorUserId
                            && c.User != null
                            && c.User.IsActive)
                .Select(c => c.UserId!.Value);

            recipientQuery = memberIds
                .Union(assigneeIds)
                .Union(commenterIds);
        }

        List<Guid> recipientIds = await recipientQuery.ToListAsync(cancellationToken);

        if (recipientIds.Count > 0)
        {
            await Context.Set<TaskReadState>().AddRangeAsync(
                recipientIds.Select(userId => new TaskReadState
                {
                    Comment = comment,
                    UserId = userId,
                }),
                cancellationToken);
        }
    }

    /// <inheritdoc />
    public Task MarkAsReadAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default)
        => Context.Set<TaskReadState>()
            .Where(rs => rs.Comment.TaskId == taskId && rs.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
}
