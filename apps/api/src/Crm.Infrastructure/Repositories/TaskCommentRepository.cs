using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides SQL-projected repository operations for managing <see cref="TaskComment"/> entities
/// and <see cref="TaskReadState"/> unread markers.
/// </summary>
/// <param name="appDbContext">The application database context.</param>
public class TaskCommentRepository(ApplicationDbContext appDbContext)
    : GenericRepository<TaskComment>(appDbContext),
    ITaskCommentRepository
{
    /// <inheritdoc />
    public async Task<List<TResult>> GetByTaskIdAsync<TResult>(Guid taskId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(c => c.TaskId == taskId)
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .ProjectToType<TResult>()
            .ToListAsync(cancellationToken);
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

        List<Guid> recipientIds = await Context.Set<User>()
            .AsNoTracking()
            .Where(u => u.IsActive
                        && u.Id != authorUserId
                        && (Context.Set<TaskUser>().Any(tu => tu.TaskId == comment.TaskId && tu.UserId == u.Id)
                            || Context.Set<ProjectTask>().Any(t => t.Id == comment.TaskId
                                && (t.Project.CreatedBy == u.Id
                                    || t.Project.Members.Any(m => m.UserId == u.Id)))
                            || DbSet.Any(c => c.TaskId == comment.TaskId && c.UserId == u.Id)))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

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
