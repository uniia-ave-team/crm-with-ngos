using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Defines repository operations for managing <see cref="TaskComment"/> entities and <see cref="TaskReadState"/> markers.
/// </summary>
public interface ITaskCommentRepository : IGenericRepository<TaskComment>
{
    /// <summary>
    /// Asynchronously retrieves and projects a paginated list of comments for a specific task, ordered chronologically by creation date.
    /// </summary>
    /// <typeparam name="TResult">The target DTO or projection type.</typeparam>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="pageNumber">The 1-based page number to retrieve.</param>
    /// <param name="pageSize">The maximum number of items per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated chronological result of projected task comments.</returns>
    Task<PagedResult<TResult>> GetPagedByTaskIdAsync<TResult>(
        Guid taskId,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a comment by task ID and comment ID with change tracking enabled.
    /// </summary>
    /// <param name="taskId">The unique identifier of the parent task.</param>
    /// <param name="commentId">The unique identifier of the comment.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The tracked <see cref="TaskComment"/> entity.</returns>
    /// <exception cref="EntityNotFoundException{TaskComment}">Thrown if the comment is not found on the task.</exception>
    Task<TaskComment> GetByTaskAndIdForUpdateAsync(Guid taskId, Guid commentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously creates unread <see cref="TaskReadState"/> markers for active task participants other than <paramref name="authorUserId"/>.
    /// </summary>
    /// <param name="comment">The newly created comment entity.</param>
    /// <param name="authorUserId">The unique identifier of the comment author to exclude.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MarkUnreadForOtherUsersAsync(
        TaskComment comment,
        Guid authorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously deletes all unread <see cref="TaskReadState"/> markers for <paramref name="userId"/> on comments belonging to <paramref name="taskId"/>.
    /// </summary>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="userId">The unique identifier of the user who read the comments.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous delete operation.</returns>
    Task MarkAsReadAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default);
}
