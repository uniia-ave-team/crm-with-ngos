using System.Linq.Expressions;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Expressions;
using Crm.Infrastructure.Persistence.Extensions;
using Crm.Infrastructure.Persistence.Sorting;
using LinqKit;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides repository implementation for managing <see cref="AuthUser"/> entities.
/// </summary>
/// <param name="appDbContext">The application database context.</param>
public class AuthUserRepository(
    ApplicationDbContext appDbContext)
    : GenericRepository<AuthUser>(appDbContext),
    IAuthUserRepository
{
    /// <inheritdoc />
    public Task<int> GetCountByRoleIdAsync(Guid roleId, CancellationToken cancellationToken = default)
        => Context.Set<AuthUserRole>().AsNoTracking().CountAsync(ur => ur.RoleId == roleId, cancellationToken);

    /// <inheritdoc />
    public async Task<TResult> GetAsync<TResult>(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.ToUpperInvariant();

        return await DbSet
            .AsNoTracking()
            .Where(x => x.NormalizedEmail == normalizedEmail)
            .ProjectToType<TResult>()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException<AuthUser>(email);
    }

    /// <inheritdoc />
    public async Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        string? searchTerm = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        bool showDeleted = false,
        string? customFieldSortKey = null,
        IReadOnlyList<string>? searchablePublicCustomFieldKeys = null,
        IReadOnlyList<string>? searchablePrivateCustomFieldKeys = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<AuthUser> query = DbSet.AsNoTracking();

        Expression<Func<AuthUser, bool>>? basePredicate = BuildUserFilter(
            searchTerm,
            showDeleted,
            searchablePublicCustomFieldKeys,
            searchablePrivateCustomFieldKeys,
            currentUserId);

        if (basePredicate is not null)
        {
            query = query.Where(basePredicate);
        }

        if (!string.IsNullOrWhiteSpace(customFieldSortKey))
        {
            string key = customFieldSortKey;
            InferredSortKind sortKind = await query
                .OrderBy(u => u.Id)
                .DetectSortKindAsync(
                    u => JsonbFunctionsExtensions.JsonbExtractPathText(u.UserProfile.CustomFields, key),
                    cancellationToken);

            query = query.OrderByUserCustomField(key, sortOrder, sortKind);
        }
        else
        {
            query = string.IsNullOrWhiteSpace(orderBy)
                ? query.OrderBy(u => u.Id)
                : ApplyOrdering<TResult>(query, orderBy, sortOrder);
        }

        return await ToPagedResultAsync<TResult>(query, pageNumber, pageSize, cancellationToken);
    }

    /// <inheritdoc />
    public async Task EnsureDoesNotExistAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.ToUpperInvariant();

        bool exists = await DbSet
            .AsNoTracking()
            .AnyAsync(m => m.NormalizedEmail == normalizedEmail, cancellationToken);

        if (exists)
        {
            throw new EntityAlreadyExistsException(nameof(AuthUser), email);
        }
    }

    /// <summary>
    /// Applies dynamic ordering to the user query, adding custom support for composite full-name sorting.
    /// </summary>
    /// <typeparam name="TResult">The projection target type.</typeparam>
    /// <param name="query">The input queryable sequence of users.</param>
    /// <param name="orderBy">The property name to sort by.</param>
    /// <param name="sortOrder">The sort direction specifier.</param>
    /// <returns>The sorted queryable sequence.</returns>
    protected override IQueryable<AuthUser> ApplyOrdering<TResult>(
        IQueryable<AuthUser> query,
        string orderBy,
        string? sortOrder)
    {
        bool isDescending = sortOrder.IsDescending();
        string normalizedOrderBy = orderBy.Trim();

        return normalizedOrderBy switch
        {
            _ when normalizedOrderBy.EqualsAnyIgnoreCase(UserConstants.FullName, nameof(User.LastName)) => isDescending
                ? query.OrderByDescending(u => u.UserProfile.LastName)
                    .ThenByDescending(u => u.UserProfile.FirstName)
                    .ThenByDescending(u => u.UserProfile.Patronymic)
                    .ThenBy(u => u.Id)
                : query.OrderBy(u => u.UserProfile.LastName)
                    .ThenBy(u => u.UserProfile.FirstName)
                    .ThenBy(u => u.UserProfile.Patronymic)
                    .ThenBy(u => u.Id),

            _ when normalizedOrderBy.EqualsAnyIgnoreCase(nameof(User.FirstName)) => isDescending
                ? query.OrderByDescending(u => u.UserProfile.FirstName)
                    .ThenByDescending(u => u.UserProfile.LastName)
                    .ThenByDescending(u => u.UserProfile.Patronymic)
                    .ThenBy(u => u.Id)
                : query.OrderBy(u => u.UserProfile.FirstName)
                    .ThenBy(u => u.UserProfile.LastName)
                    .ThenBy(u => u.UserProfile.Patronymic)
                    .ThenBy(u => u.Id),

            _ when normalizedOrderBy.EqualsAnyIgnoreCase(nameof(User.CreatedAt)) => isDescending
                ? query.OrderByDescending(u => u.UserProfile.CreatedAt).ThenBy(u => u.Id)
                : query.OrderBy(u => u.UserProfile.CreatedAt).ThenBy(u => u.Id),

            _ => base.ApplyOrdering<TResult>(query, normalizedOrderBy, sortOrder),
        };
    }

    private static Expression<Func<AuthUser, bool>>? BuildUserFilter(
        string? searchTerm,
        bool showDeleted,
        IReadOnlyList<string>? searchablePublicCustomFieldKeys,
        IReadOnlyList<string>? searchablePrivateCustomFieldKeys,
        Guid? currentUserId)
    {
        Expression<Func<AuthUser, bool>>? activeFilter = showDeleted ? null : u => u.UserProfile.IsActive;

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return activeFilter;
        }

        Expression<Func<AuthUser, bool>> profileSearchFilter = QueryFilterExpressions.ContainsIgnoreCase<AuthUser>(
            searchTerm,
            u => u.UserProfile.FirstName,
            u => u.UserProfile.LastName,
            u => u.UserProfile.Patronymic,
            u => u.UserProfile.PhoneNumber,
            u => u.UserProfile.Country,
            u => u.Email);

        Expression<Func<AuthUser, bool>>? customFieldSearchFilter = UserCustomFieldQueryExtensions
            .BuildVisibleCustomFieldSearchPredicate(
                searchTerm,
                searchablePublicCustomFieldKeys,
                searchablePrivateCustomFieldKeys,
                currentUserId);

        Expression<Func<AuthUser, bool>> searchFilter = customFieldSearchFilter is null
            ? profileSearchFilter
            : profileSearchFilter.Or(customFieldSearchFilter).Expand();

        return activeFilter is null
            ? searchFilter
            : activeFilter.And(searchFilter).Expand();
    }
}
