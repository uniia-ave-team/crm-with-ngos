using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Defines repository operations for managing <see cref="Topic"/> entities (board columns/sections).
/// </summary>
public interface ITopicRepository : IGenericRepository<Topic>
{
    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of board topics belonging to a project.
    /// </summary>
    /// <typeparam name="TResult">The target DTO or projection type.</typeparam>
    /// <param name="projectId">The unique identifier of the parent project.</param>
    /// <param name="searchTerm">An optional search term matched against topic title.</param>
    /// <param name="orderBy">An optional property name to sort the results by.</param>
    /// <param name="sortOrder">The sort direction (<c>asc</c> or <c>desc</c>).</param>
    /// <param name="pageNumber">The 1-based page number to retrieve.</param>
    /// <param name="pageSize">The maximum number of items per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated result containing the projected topics and pagination metadata.</returns>
    Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        Guid projectId,
        string? searchTerm = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a topic by project ID and topic ID with change tracking enabled.
    /// </summary>
    /// <param name="projectId">The unique identifier of the parent project.</param>
    /// <param name="topicId">The unique identifier of the topic.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The tracked <see cref="Topic"/> entity.</returns>
    /// <exception cref="EntityNotFoundException{Topic}">Thrown if the topic is not found in the project.</exception>
    Task<Topic> GetByProjectAndIdForUpdateAsync(Guid projectId, Guid topicId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves all board topics belonging to a project with change tracking enabled, ordered by <see cref="Topic.Order"/>.
    /// </summary>
    /// <param name="projectId">The unique identifier of the parent project.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tracked list of all topics in the project ordered by display position.</returns>
    Task<List<Topic>> GetByProjectIdForUpdateAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves the existing display order values of all topics in the specified project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the parent project.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The list of existing 0-based topic order values.</returns>
    Task<List<byte>> GetOrdersByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves temporary unique index collisions on <c>(ProjectId, Order)</c> before saving reordered or compacted topics.
    /// </summary>
    /// <param name="projectId">The unique identifier of the parent project.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous collision resolution.</returns>
    Task ResolveOrderCollisionsAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously ensures that a topic exists within the specified project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the parent project.</param>
    /// <param name="topicId">The unique identifier of the topic.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous existence check.</returns>
    /// <exception cref="EntityNotFoundException{Topic}">Thrown if the topic does not exist in the project.</exception>
    Task EnsureExistsInProjectAsync(Guid projectId, Guid topicId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously determines whether a topic with the given title already exists in the project (case-insensitive).
    /// </summary>
    /// <param name="projectId">The unique identifier of the parent project.</param>
    /// <param name="title">The topic title to check.</param>
    /// <param name="excludeTopicId">An optional topic ID to exclude (for updates).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> if a conflicting topic title exists; otherwise, <c>false</c>.</returns>
    Task<bool> ExistsWithTitleAsync(
        Guid projectId,
        string title,
        Guid? excludeTopicId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously determines whether a topic with the given display order already exists in the project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the parent project.</param>
    /// <param name="order">The display order to check.</param>
    /// <param name="excludeTopicId">An optional topic ID to exclude (for updates).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> if a conflicting topic order exists; otherwise, <c>false</c>.</returns>
    Task<bool> ExistsWithOrderAsync(
        Guid projectId,
        byte order,
        Guid? excludeTopicId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously determines whether any tasks are currently assigned to the specified topic.
    /// </summary>
    /// <param name="topicId">The unique identifier of the topic.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> if at least one task is assigned to the topic; otherwise, <c>false</c>.</returns>
    Task<bool> HasTasksAsync(Guid topicId, CancellationToken cancellationToken = default);
}
