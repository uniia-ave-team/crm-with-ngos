using Crm.Domain.Common;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Defines repository operations for managing <see cref="ProjectTask"/> entities, SQL-projected queries,
/// and unread comment tracking.
/// </summary>
public interface IProjectTaskRepository : IGenericRepository<ProjectTask>
{
    /// <summary>
    /// Asynchronously retrieves and projects a single task belonging to the specified project.
    /// </summary>
    /// <typeparam name="TResult">The target DTO or projection type.</typeparam>
    /// <param name="projectId">The unique identifier of the parent project.</param>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The projected task instance.</returns>
    /// <exception cref="EntityNotFoundException{ProjectTask}">Thrown if the task is not found in the project.</exception>
    Task<TResult> GetByProjectAndIdAsync<TResult>(Guid projectId, Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, natively sorted, and SQL-projected list of tasks belonging to a project.
    /// </summary>
    /// <typeparam name="TResult">The target DTO or projection type.</typeparam>
    /// <param name="filter">The task filter criteria specifying project scope, personal view, tags, status, priority, dates, and search term.</param>
    /// <param name="currentUserId">The unique identifier of the current user (used for personal filtering and unread comment sorting).</param>
    /// <param name="orderBy">An optional property name to sort the results by.</param>
    /// <param name="sortOrder">The sort direction (<c>asc</c> or <c>desc</c>).</param>
    /// <param name="pageNumber">The 1-based page number to retrieve.</param>
    /// <param name="pageSize">The maximum number of items per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated result containing the projected tasks and pagination metadata.</returns>
    Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        ProjectTaskFilter filter,
        Guid currentUserId,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously computes unread comment counts for the specified task IDs and user in a single grouped SQL query.
    /// </summary>
    /// <param name="taskIds">The collection of task identifiers to inspect.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A dictionary mapping each task ID with unread comments to its unread count.</returns>
    Task<Dictionary<Guid, int>> GetUnreadCommentCountsAsync(
        IEnumerable<Guid> taskIds,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a tracked <see cref="ProjectTask"/> within a project for scalar updates.
    /// </summary>
    /// <param name="projectId">The unique identifier of the parent project.</param>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The tracked <see cref="ProjectTask"/> entity.</returns>
    /// <exception cref="EntityNotFoundException{ProjectTask}">Thrown if the task is not found in the project.</exception>
    Task<ProjectTask> GetByProjectAndIdForUpdateAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously ensures that a task exists within the specified project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the parent project.</param>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous existence check.</returns>
    /// <exception cref="EntityNotFoundException{ProjectTask}">Thrown if the task does not exist in the project.</exception>
    Task EnsureExistsInProjectAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously updates the <see cref="ProjectTask.UpdatedAt"/> timestamp of a task in the database.
    /// </summary>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="updatedAt">The UTC timestamp to persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous update operation.</returns>
    Task UpdateUpdatedAtAsync(Guid taskId, DateTime updatedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes task assignments (<see cref="TaskUser"/>) and read states (<see cref="TaskReadState"/>)
    /// across all tasks in <paramref name="projectId"/> for users who are neither the project's creator nor a project member.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous cleanup operation.</returns>
    Task RemoveInaccessibleUserDataAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously assigns the specified user to a task if not already assigned.
    /// </summary>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="userId">The unique identifier of the user to assign.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> if the assignment was created; <c>false</c> if already assigned.</returns>
    Task<bool> AssignToTaskAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously assigns all specified users to a newly created task.
    /// </summary>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="userIds">The collection of user identifiers to assign.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AssignToTaskAsync(Guid taskId, IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously synchronizes assigned users on an existing task to match <paramref name="userIds"/>.
    /// </summary>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="userIds">The target collection of assigned user identifiers.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous synchronization.</returns>
    Task SyncTaskUsersAsync(Guid taskId, IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously unassigns the specified user from a task if assigned.
    /// </summary>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="userId">The unique identifier of the user to unassign.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> if the assignment was removed; <c>false</c> if the user was not assigned.</returns>
    Task<bool> UnassignFromTaskAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default);
}
