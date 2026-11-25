using System.Linq.Expressions;
using Crm.Application.Dtos.Projects;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Auditing;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Expressions;
using Crm.Infrastructure.Persistence.Sorting;
using LinqKit;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides SQL-projected repository operations for managing <see cref="Project"/> and <see cref="ProjectMember"/> entities.
/// </summary>
/// <param name="appDbContext">The application database context.</param>
public class ProjectRepository(ApplicationDbContext appDbContext)
    : GenericRepository<Project>(appDbContext),
    IProjectRepository
{
    /// <inheritdoc />
    public Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        Guid userId,
        string? searchTerm = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => GetPagedAsync<TResult>(
            predicate: BuildVisibleProjectFilter(userId, searchTerm),
            orderBy: orderBy,
            sortOrder: sortOrder,
            pageNumber: pageNumber,
            pageSize: pageSize,
            cancellationToken: cancellationToken);

    /// <inheritdoc />
    public Task<ProjectAccess?> GetAccessAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => BuildAccessQuery(projectId, userId).FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<ProjectClaim?> GetMemberClaimAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await Context.Set<ProjectMember>()
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId && m.UserId == userId)
            .Select(m => (ProjectClaim?)m.Claim)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> HasAnotherManagerAsync(
        Guid projectId,
        Guid excludeUserId,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .AnyAsync(
                p => p.Members.Any(m => m.UserId != excludeUserId && m.Claim >= ProjectClaim.Manage && m.User.IsActive),
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<Guid>> GetSoleManagedProjectIdsForUpdateAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        List<Guid> managedProjectIds = await LockIdsForUpdateAsync(
            p => p.Members.Any(m => m.UserId == userId && m.Claim >= ProjectClaim.Manage),
            cancellationToken);

        return managedProjectIds.Count == 0
            ? []
            : await DbSet
                .AsNoTracking()
                .Where(p => managedProjectIds.Contains(p.Id)
                            && !p.Members.Any(m => m.UserId != userId && m.Claim >= ProjectClaim.Manage && m.User.IsActive))
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task EnsureManagerForProjectsAsync(
        IEnumerable<Guid> projectIds,
        Guid newManagerUserId,
        CancellationToken cancellationToken = default)
    {
        List<Guid> distinctProjectIds = [.. projectIds.Distinct()];
        if (distinctProjectIds.Count == 0)
        {
            return;
        }

        var memberSet = Context.Set<ProjectMember>();
        var existingMembers = await memberSet
            .Where(m => distinctProjectIds.Contains(m.ProjectId) && m.UserId == newManagerUserId)
            .ToListAsync(cancellationToken);

        var existingByProjectId = existingMembers.ToDictionary(m => m.ProjectId);

        foreach (Guid projectId in distinctProjectIds)
        {
            if (existingByProjectId.TryGetValue(projectId, out var existingMember))
            {
                if (existingMember.Claim < ProjectClaim.Manage)
                {
                    existingMember.Claim = ProjectClaim.Manage;
                }
            }
            else
            {
                await memberSet.AddAsync(
                    new ProjectMember
                    {
                        ProjectId = projectId,
                        UserId = newManagerUserId,
                        Claim = ProjectClaim.Manage,
                        RoleTitle = string.Empty,
                    },
                    cancellationToken);
            }
        }
    }

    /// <inheritdoc />
    public async Task<HashSet<Guid>> GetMemberUserIdsAsync(
        Guid projectId,
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        List<Guid> distinctIds = [.. userIds.Distinct()];
        return distinctIds.Count == 0
            ? []
            : [
                .. await Context.Set<ProjectMember>()
                .AsNoTracking()
                .Where(m => m.ProjectId == projectId && distinctIds.Contains(m.UserId))
                .Select(m => m.UserId)
                .ToListAsync(cancellationToken),
              ];
    }

    /// <inheritdoc />
    public async Task<List<TResult>> GetMembersAsync<TResult>(Guid projectId, CancellationToken cancellationToken = default)
    {
        return await Context.Set<ProjectMember>()
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId)
            .OrderBy(m => m.User.LastName)
            .ThenBy(m => m.User.FirstName)
            .ThenBy(m => m.UserId)
            .ProjectToType<TResult>()
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> MemberExistsAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await Context.Set<ProjectMember>()
            .AsNoTracking()
            .AnyAsync(m => m.ProjectId == projectId && m.UserId == userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ProjectMember> GetMemberForUpdateAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await Context.Set<ProjectMember>()
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId, cancellationToken)
                ?? throw new EntityNotFoundException<ProjectMember>(userId);
    }

    /// <inheritdoc />
    public async Task AddMemberAsync(ProjectMember member, CancellationToken cancellationToken = default)
    {
        await Context.Set<ProjectMember>().AddAsync(member, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Delete tasks first because Topic -> ProjectTask uses Restrict delete behavior,
        // which would block Project -> Topic cascade deletion in PostgreSQL.
        await Context.Set<ProjectTask>()
            .Where(t => t.ProjectId == id)
            .ExecuteDeleteAsync(cancellationToken);

        await base.DeleteAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        int rowsAffected = await Context.Set<ProjectMember>()
            .Where(m => m.ProjectId == projectId && m.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        if (rowsAffected == 0)
        {
            throw new EntityNotFoundException<ProjectMember>(userId);
        }

        Context.TryEnqueueBulkDeleteAudit<ProjectMember>($"{projectId}:{userId}");
    }

    /// <summary>
    /// Builds a SQL-translatable filter predicate for projects visible to <paramref name="userId"/>
    /// by reusing <see cref="ProjectVisibilityExpressions.IsVisibleTo"/>.
    /// </summary>
    /// <param name="userId">The unique identifier of the user requesting projects.</param>
    /// <param name="searchTerm">An optional search term matched against project title and description.</param>
    /// <returns>An expression filtering visible projects matching the optional search term.</returns>
    protected static Expression<Func<Project, bool>> BuildVisibleProjectFilter(Guid userId, string? searchTerm)
    {
        Expression<Func<Project, bool>> visibilityFilter = ProjectVisibilityExpressions.IsVisibleTo(userId);
        return string.IsNullOrWhiteSpace(searchTerm)
            ? visibilityFilter
            : visibilityFilter.And(
                QueryFilterExpressions.ContainsIgnoreCase<Project>(searchTerm, p => p.Title, p => p.Description))
                .Expand();
    }

    /// <summary>
    /// Applies project ordering with null-safe <see cref="ProjectSummaryDto.EndDate"/> sorting
    /// and native <see cref="GenericRepository{T}.ApplyOrdering{TResult}"/> fallback.
    /// </summary>
    /// <typeparam name="TResult">The target DTO or projection type.</typeparam>
    /// <param name="query">The source project queryable.</param>
    /// <param name="orderBy">The requested DTO sort property name.</param>
    /// <param name="sortOrder">The sort direction (<c>asc</c> or <c>desc</c>).</param>
    /// <returns>The ordered project queryable.</returns>
    protected override IQueryable<Project> ApplyOrdering<TResult>(
        IQueryable<Project> query,
        string orderBy,
        string? sortOrder)
    {
        string normalized = orderBy.Trim();
        return string.Equals(normalized, nameof(ProjectSummaryDto.EndDate), StringComparison.OrdinalIgnoreCase)
            ? query.ApplyNullsLastOrdering(p => p.EndDate, p => p.Id, sortOrder.IsDescending(), nullsLastInAscending: true)
            : base.ApplyOrdering<TResult>(query, normalized, sortOrder);
    }

    /// <summary>
    /// Builds a lightweight SQL-translatable projection of project visibility and user membership claim.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>A queryable projection of <see cref="ProjectAccess"/>.</returns>
    protected IQueryable<ProjectAccess> BuildAccessQuery(Guid projectId, Guid userId)
        => DbSet
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new ProjectAccess(
                p.Visibility,
                p.Members
                    .Where(m => m.UserId == userId)
                    .Select(m => (ProjectClaim?)m.Claim)
                    .FirstOrDefault(),
                userId));
}
