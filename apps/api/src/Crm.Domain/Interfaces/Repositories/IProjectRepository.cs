using Crm.Domain.Common;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Defines repository operations for managing <see cref="Project"/> entities and <see cref="ProjectMember"/> assignments.
/// </summary>
public interface IProjectRepository : IGenericRepository<Project>
{
    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of projects visible to <paramref name="userId"/>.
    /// </summary>
    /// <typeparam name="TResult">The target DTO or projection type.</typeparam>
    /// <param name="userId">The unique identifier of the user requesting visible projects.</param>
    /// <param name="searchTerm">An optional search term matched against project title and description.</param>
    /// <param name="orderBy">An optional property name to sort the results by.</param>
    /// <param name="sortOrder">The sort direction (<c>asc</c> or <c>desc</c>).</param>
    /// <param name="pageNumber">The 1-based page number to retrieve.</param>
    /// <param name="pageSize">The maximum number of items per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated result containing the projected items and pagination metadata.</returns>
    Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        Guid userId,
        string? searchTerm = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves pure project access metadata for evaluating view, edit, and management permissions
    /// without throwing authorization exceptions.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="userId">The unique identifier of the user to evaluate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="ProjectAccess"/> record if the project exists; otherwise, <c>null</c>.
    /// </returns>
    Task<ProjectAccess?> GetAccessAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves the specified user's permission claim on the project, if they are a member.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The user's <see cref="ProjectClaim"/> on the project, or <c>null</c> if not a member.</returns>
    Task<ProjectClaim?> GetMemberClaimAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously checks whether the project has at least one active member with
    /// <see cref="ProjectClaim.Manage"/> other than <paramref name="excludeUserId"/>.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="excludeUserId">The unique identifier of the user being removed or demoted.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> if another active manager remains on the project; otherwise, <c>false</c>.</returns>
    Task<bool> HasAnotherManagerAsync(
        Guid projectId,
        Guid excludeUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously locks all projects managed by <paramref name="userId"/> in deterministic order within the active
    /// transaction and returns the identifiers of projects where <paramref name="userId"/> is the sole remaining active manager.
    /// </summary>
    /// <param name="userId">The unique identifier of the user being deactivated or deleted.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of project identifiers where <paramref name="userId"/> is the sole active manager.</returns>
    Task<List<Guid>> GetSoleManagedProjectIdsForUpdateAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously ensures that <paramref name="newManagerUserId"/> holds at least <see cref="ProjectClaim.Manage"/>
    /// on each project in <paramref name="projectIds"/>, promoting existing memberships or inserting new ones as needed.
    /// </summary>
    /// <param name="projectIds">The identifiers of projects to ensure management continuity on.</param>
    /// <param name="newManagerUserId">The unique identifier of the active user taking over project management.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EnsureManagerForProjectsAsync(
        IEnumerable<Guid> projectIds,
        Guid newManagerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves the subset of <paramref name="userIds"/> that are explicit members of the project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="userIds">The collection of user identifiers to check.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A set of user identifiers who are members of the project.</returns>
    Task<HashSet<Guid>> GetMemberUserIdsAsync(
        Guid projectId,
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves and projects all members belonging to a specific project, ordered by user name.
    /// </summary>
    /// <typeparam name="TResult">The target DTO or projection type.</typeparam>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of projected project member records.</returns>
    Task<List<TResult>> GetMembersAsync<TResult>(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously determines whether the specified user is already assigned as a member of the project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> if the user is a member of the project; otherwise, <c>false</c>.</returns>
    Task<bool> MemberExistsAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a specific project member record with change tracking enabled for update.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="userId">The unique identifier of the project member user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The tracked <see cref="ProjectMember"/> entity.</returns>
    /// <exception cref="EntityNotFoundException{ProjectMember}">Thrown if the member assignment is not found.</exception>
    Task<ProjectMember> GetMemberForUpdateAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously adds a new member to a project.
    /// </summary>
    /// <param name="member">The project member entity to add.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddMemberAsync(ProjectMember member, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes a member from a project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="userId">The unique identifier of the user to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="EntityNotFoundException{ProjectMember}">Thrown if the member assignment does not exist.</exception>
    Task RemoveMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
}
