using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Crm.Domain.Common;
using Crm.Domain.Consts;
using Crm.Domain.Consts.Entities.GeneralConstants;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides repository implementation for managing <see cref="AuthUser"/> entities.
/// </summary>
public class AuthUserRepository(
    ApplicationDbContext appDbContext)
    : GenericRepository<AuthUser>(appDbContext),
    IAuthUserRepository
{
    /// <summary>
    /// Asynchronously gets the total count of users assigned to a specific role.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of users that have the specified role assigned.</returns>
    public Task<int> GetCountByRoleIdAsync(Guid roleId, CancellationToken cancellationToken = default)
        => Context.Set<AuthUserRole>().CountAsync(ur => ur.RoleId == roleId, cancellationToken);

    /// <summary>
    /// Asynchronously retrieves and projects an entity by its email address using Mapster.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected element (e.g., DTO).</typeparam>
    /// <param name="email">The email address of the entity to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The projected entity DTO associated with the specified email address.</returns>
    /// <exception cref="NotFoundException">Thrown if the entity with the specified email address is not found.</exception>
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

    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of projected entities based on the search term.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected elements (e.g., DTO).</typeparam>
    /// <param name="searchTerm">Optional search term to filter entities (e.g., by name, email, or other relevant fields).</param>
    /// <param name="orderBy">The name of the property to sort by.</param>
    /// <param name="sortOrder">The sort direction (e.g., <see cref="SortOrderConstants.Ascending"/> or <see cref="SortOrderConstants.Descending"/>). Defaults to <see cref="SortOrderConstants.Ascending"/>.</param>
    /// <param name="pageNumber">The current page number (1-based index). Defaults to 1.</param>
    /// <param name="pageSize">The number of items per page. Defaults to 10.</param>
    /// <param name="showDeleted">A value indicating whether to include soft-deleted entities in the results. Defaults to false.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a paginated collection of projected items along with pagination metadata.</returns>
    public Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        string? searchTerm = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        bool showDeleted = false,
        CancellationToken cancellationToken = default)
        => GetPagedAsync<TResult>(
            predicate: BuildUserFilter(searchTerm, showDeleted),
            orderBy: orderBy,
            sortOrder: sortOrder,
            pageNumber: pageNumber,
            pageSize: pageSize,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Asynchronously ensures that an entity with the specified email address does not already exist.
    /// Throws an exception if an entity with this email is found.
    /// </summary>
    /// <param name="email">The email address to check for uniqueness.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous validation operation.</returns>
    /// <exception cref="EntityAlreadyExistsException">Thrown if an entity with the specified email already exists.</exception>
    public async Task EnsureNotExistsAsync(string email, CancellationToken cancellationToken = default)
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
        bool isDescending = string.Equals(sortOrder, SortOrderConstants.Descending, StringComparison.OrdinalIgnoreCase);
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

            // TODO: Support dynamic sorting on user custom fields with SQL type casting (Date, Number, Text) similar to CustomRecordRepository.
            _ => base.ApplyOrdering<TResult>(query, normalizedOrderBy, sortOrder),
        };
    }

    /// <summary>
    /// Builds the filter expression for querying users based on search term and deletion status.
    /// </summary>
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF Core translates this to SQL")]
    [SuppressMessage("Globalization", "CA1307:Specify StringComparison for clarity", Justification = "EF Core translates this to SQL")]
    [SuppressMessage("Performance", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF Core requires ToLower for correct SQL translation")]
    private static Expression<Func<AuthUser, bool>> BuildUserFilter(string? searchTerm, bool showDeleted)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return u => showDeleted || u.UserProfile.IsActive;
        }

        string term = searchTerm.Trim().ToLowerInvariant();

        return u =>
            (showDeleted || u.UserProfile.IsActive) &&
            (
                u.UserProfile.FirstName.ToLower().Contains(term) ||
                u.UserProfile.LastName.ToLower().Contains(term) ||
                (u.UserProfile.Patronymic != null && u.UserProfile.Patronymic.ToLower().Contains(term)) ||
                (u.UserProfile.PhoneNumber != null && u.UserProfile.PhoneNumber.ToLower().Contains(term)) ||
                (u.UserProfile.Country != null && u.UserProfile.Country.ToLower().Contains(term)) ||
                (u.Email != null && u.Email.ToLower().Contains(term)));
    }
}
