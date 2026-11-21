using Crm.Domain.Common;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Defines repository operations for managing project-scoped <see cref="Tag"/> entities.
/// </summary>
public interface ITagRepository : IGenericRepository<Tag>
{
    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of tags belonging to a project.
    /// </summary>
    /// <typeparam name="TResult">The target DTO or projection type.</typeparam>
    /// <param name="projectId">The unique identifier of the parent project.</param>
    /// <param name="searchTerm">An optional search term matched against tag title.</param>
    /// <param name="orderBy">An optional property name to sort the results by.</param>
    /// <param name="sortOrder">The sort direction (<c>asc</c> or <c>desc</c>).</param>
    /// <param name="pageNumber">The 1-based page number to retrieve.</param>
    /// <param name="pageSize">The maximum number of items per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated result containing the projected tags and pagination metadata.</returns>
    Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        Guid projectId,
        string? searchTerm = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a tag by project ID and tag ID with change tracking enabled.
    /// </summary>
    /// <param name="projectId">The unique identifier of the parent project.</param>
    /// <param name="tagId">The unique identifier of the tag.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The tracked <see cref="Tag"/> entity.</returns>
    /// <exception cref="EntityNotFoundException{Tag}">Thrown if the tag does not exist in the project.</exception>
    Task<Tag> GetByProjectAndIdForUpdateAsync(Guid projectId, Guid tagId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously checks if a tag with the given title already exists in the project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the parent project.</param>
    /// <param name="title">The tag title to check.</param>
    /// <param name="excludeTagId">An optional tag ID to exclude from the uniqueness check when updating an existing tag.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> if a matching tag already exists in the project; otherwise, <c>false</c>.</returns>
    Task<bool> ExistsWithTitleAsync(Guid projectId, string title, Guid? excludeTagId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously ensures that a tag exists within the specified project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the parent project.</param>
    /// <param name="tagId">The unique identifier of the tag.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous existence check.</returns>
    /// <exception cref="EntityNotFoundException{Tag}">Thrown if the tag does not exist in the project.</exception>
    Task EnsureExistsInProjectAsync(Guid projectId, Guid tagId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously ensures that all specified tag IDs exist within the given project without loading entities.
    /// </summary>
    /// <param name="projectId">The unique identifier of the parent project.</param>
    /// <param name="tagIds">The collection of tag identifiers to verify.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous validation.</returns>
    /// <exception cref="EntitiesNotFoundException">Thrown if one or more tag IDs do not exist in the project.</exception>
    Task EnsureAllExistInProjectAsync(Guid projectId, IEnumerable<Guid> tagIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously associates a tag with the specified task if not already associated.
    /// </summary>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="tagId">The unique identifier of the tag to associate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> if the tag association was created; <c>false</c> if it already existed.</returns>
    Task<bool> AddToTaskAsync(Guid taskId, Guid tagId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously associates all specified tags with a newly created task.
    /// </summary>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="tagIds">The collection of tag identifiers to associate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddToTaskAsync(Guid taskId, IEnumerable<Guid> tagIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously synchronizes tag associations on an existing task to match <paramref name="tagIds"/>.
    /// </summary>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="tagIds">The target collection of tag identifiers.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous synchronization.</returns>
    Task SyncTaskTagsAsync(Guid taskId, IEnumerable<Guid> tagIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes a tag association from the specified task if present.
    /// </summary>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="tagId">The unique identifier of the tag to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> if the tag association was removed; <c>false</c> if it was not present.</returns>
    Task<bool> RemoveFromTaskAsync(Guid taskId, Guid tagId, CancellationToken cancellationToken = default);
}
