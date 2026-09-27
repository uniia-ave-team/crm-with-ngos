using System.Collections.Concurrent;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using Crm.Domain.Consts;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces;
using Crm.Domain.Interfaces.Repositories.Generic;
using Crm.Infrastructure.Helpers;
using Crm.Infrastructure.Persistence;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides a generic repository implementation for basic CRUD operations on entities.
/// </summary>
/// <typeparam name="T">The type of entity managed by the repository. Must implement <see cref="IEntity"/>.</typeparam>
public abstract class GenericRepository<T>(ApplicationDbContext context)
    : IGenericRepository<T>
    where T : class, IEntity
{
    /// <summary>
    /// Thread-safe cache mapping DTO properties to their corresponding Entity navigation paths (via Mapster).
    /// Prevents reflection and expression parsing overhead during runtime.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Dictionary<string, string>> _dtoSortMappingCache = new();

    /// <summary>
    /// Case-insensitive whitelist of allowed sort direction specifiers ("asc" / "desc") for dynamic sorting validation.
    /// </summary>
    private static readonly HashSet<string> _allowedSortOrders = new(StringComparer.OrdinalIgnoreCase)
    {
        SortOrderConstants.Ascending,
        SortOrderConstants.Descending,
    };

    /// <summary>
    /// Gets the <see cref="DbSet{TEntity}"/> for the entity type <typeparamref name="T"/>.
    /// </summary>
    protected DbSet<T> DbSet { get; } = context.Set<T>();

    /// <summary>
    /// Gets the <see cref="DbContext"/>.
    /// </summary>
    protected DbContext Context { get; } = context;

    /// <summary>
    /// Asynchronously creates a new entity in the database.
    /// </summary>
    /// <param name="entity">The entity to create.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the newly created entity.</returns>
    public async Task<T> CreateAsync(T entity, CancellationToken cancellationToken = default)
    {
        var entry = await DbSet.AddAsync(entity, cancellationToken);
        return entry.Entity;
    }

    /// <summary>
    /// Asynchronously deletes an entity by its unique identifier using a bulk delete operation.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the entity with the specified ID is not found.</exception>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await DbSet
                    .Where(x => x.Id == id)
                    .ExecuteDeleteAsync(cancellationToken);

        if (rowsAffected == 0)
        {
            throw new EntityNotFoundException(typeof(T).Name, id);
        }
    }

    /// <summary>
    /// Asynchronously retrieves an entity by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The entity with the specified ID.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the entity with the specified ID is not found.</exception>
    public async Task<T> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException(typeof(T).Name, id);
    }

    /// <summary>
    /// Asynchronously retrieves and projects an entity by its unique identifier using Mapster.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected element (e.g., DTO).</typeparam>
    /// <param name="id">The unique identifier of the entity to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The projected entity DTO with the specified ID.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the entity with the specified ID is not found.</exception>
    public async Task<TResult> GetAsync<TResult>(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(x => x.Id == id)
            .ProjectToType<TResult>()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException(typeof(T).Name, id);
    }

    /// <summary>
    /// Asynchronously retrieves an entity by its unique identifier with change tracking enabled.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The tracked entity with the specified ID.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the entity with the specified ID is not found.</exception>
    public async Task<T> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException(typeof(T).Name, id);
    }

    /// <summary>
    /// Asynchronously checks if an entity with the specified unique identifier exists.
    /// Throws an exception if the entity is not found.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to check.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if no entity with the specified ID exists.</exception>
    public async Task EnsureExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        bool exists = await DbSet
            .AsNoTracking()
            .AnyAsync(m => m.Id == id, cancellationToken);

        if (!exists)
        {
            throw new EntityNotFoundException(typeof(T).Name, id);
        }
    }

    /// <summary>
    /// Asynchronously ensures that entities exist for all specified unique identifiers.
    /// Throws an exception containing all missing keys if any entity is not found.
    /// </summary>
    /// <param name="ids">The collection of unique identifiers to check.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="EntitiesNotFoundException">Thrown if any entity with the specified ID is not found.</exception>
    public async Task EnsureAllExistAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return;
        }

        var existingIds = await DbSet
            .AsNoTracking()
            .Where(e => distinctIds.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        var missingIds = distinctIds.Except(existingIds).Cast<object>().ToList();
        if (missingIds.Count > 0)
        {
            throw new EntitiesNotFoundException(typeof(T).Name, missingIds);
        }
    }

    /// <summary>
    /// Asynchronously retrieves all entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of all entities.</returns>
    public async Task<List<T>> GetListAsync(CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking().ToListAsync(cancellationToken);

    /// <summary>
    /// Asynchronously retrieves a single random entity from the database and projects it to the specified type using Mapster.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected element (e.g., DTO).</typeparam>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A random projected entity.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if no entities exist in the database for this type.</exception>
    public async Task<TResult> GetRandomAsync<TResult>(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .OrderBy(x => Guid.NewGuid())
            .ProjectToType<TResult>()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException(typeof(T).Name);
    }

    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of projected entities.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected elements (e.g., DTO).</typeparam>
    /// <param name="predicate">
    /// Expression that MUST be translatable to SQL by EF Core.
    /// Non-translatable expressions will cause client-side evaluation.
    /// </param>
    /// <param name="orderBy">The name of the property to sort by.</param>
    /// <param name="sortOrder">The sort direction ("asc" or "desc").</param>
    /// <param name="pageNumber">The current page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated result containing the projected items and metadata.</returns>
    public async Task<Domain.Common.PagedResult<TResult>> GetPagedAsync<TResult>(
        Expression<Func<T, bool>>? predicate = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, PaginationConstants.MinPageSize, PaginationConstants.MaxPageSize);
        pageNumber = Math.Max(PaginationConstants.MinPageNumber, pageNumber);

        IQueryable<T> query = DbSet.AsNoTracking();

        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        int totalCount = await query.CountAsync(cancellationToken);

        int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        if (totalCount == 0 || pageNumber > totalPages)
        {
            return new([], totalCount, pageNumber, pageSize);
        }

        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            var sortMapping = GetSortMapping<TResult>();

            if (sortMapping.TryGetValue(orderBy, out string? resolvedEntityPath)
                && resolvedEntityPath.All(c => char.IsLetterOrDigit(c) || c == '.'))
            {
                string safeSortOrder = _allowedSortOrders.Contains(sortOrder ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                                    ? sortOrder
                                    : SortOrderConstants.Ascending;

                query = query.OrderBy($"{resolvedEntityPath} {safeSortOrder}");
            }
        }

        IQueryable<TResult> projectedQuery = query.ProjectToType<TResult>();

        var items = await projectedQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new(items, totalCount, pageNumber, pageSize);
    }

    private static Dictionary<string, string> GetSortMapping<TResult>() =>
        _dtoSortMappingCache.GetOrAdd(typeof(TResult), _ => MapsterSortResolver.GetSortMapping<T, TResult>());
}
