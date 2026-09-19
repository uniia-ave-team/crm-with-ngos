using System.Collections.Concurrent;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using Crm.Domain.Consts;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Helpers;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides a generic repository implementation for basic CRUD operations on entities.
/// </summary>
/// <typeparam name="T">The type of entity managed by the repository. Must implement <see cref="IEntity"/>.</typeparam>
public abstract class GenericRepository<T>(DbContext context)
    : IGenericRepository<T>
    where T : class, IEntity
{
    /// <summary>
    /// Gets the maximum allowed page size.
    /// </summary>
    protected const int MaxPageSize = 100;

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
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the newly created entity.</returns>
    public async Task<T> CreateAsync(T entity, CancellationToken ct = default)
    {
        var entry = await DbSet.AddAsync(entity, ct);
        return entry.Entity;
    }

    /// <summary>
    /// Asynchronously deletes an entity by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to delete.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the entity with the specified ID is not found.</exception>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var model = await DbSet.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new EntityNotFoundException(typeof(T).Name, id);

        DbSet.Remove(model);
    }

    /// <summary>
    /// Asynchronously retrieves an entity by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to retrieve.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>The entity with the specified ID.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the entity with the specified ID is not found.</exception>
    public async Task<T> GetAsync(Guid id, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new EntityNotFoundException(typeof(T).Name, id);
    }

    /// <summary>
    /// Asynchronously retrieves and projects an entity by its unique identifier using Mapster.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected element (e.g., DTO).</typeparam>
    /// <param name="id">The unique identifier of the entity to retrieve.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>The projected entity DTO with the specified ID.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the entity with the specified ID is not found.</exception>
    public async Task<TResult> GetAsync<TResult>(Guid id, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(x => x.Id == id)
            .ProjectToType<TResult>()
            .FirstOrDefaultAsync(ct)
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
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>A list of all entities.</returns>
    public async Task<List<T>> GetListAsync(CancellationToken ct = default)
        => await DbSet.AsNoTracking().ToListAsync(ct);

    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of projected entities.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected elements (e.g., DTO).</typeparam>
    /// <param name="predicate">Optional expression to filter the entities.</param>
    /// <param name="orderBy">The name of the property to sort by.</param>
    /// <param name="sortOrder">The sort direction ("asc" or "desc").</param>
    /// <param name="pageNumber">The current page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated result containing the projected items and metadata.</returns>
    public async Task<Domain.Common.PagedResult<TResult>> GetPagedAsync<TResult>(
        Expression<Func<T, bool>>? predicate = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        pageNumber = Math.Max(1, pageNumber);

        IQueryable<T> query = DbSet.AsNoTracking();

        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        int totalCount = await query.CountAsync(ct);

        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            var sortMapping = GetSortMapping<TResult>();

            if (sortMapping.TryGetValue(orderBy, out string? resolvedEntityPath))
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
            .ToListAsync(ct);

        return new(items, totalCount, pageNumber, pageSize);
    }

    /// <summary>
    /// Updates an existing entity in the data store's tracking state.
    /// </summary>
    /// <param name="entity">
    /// The entity instance containing updated values.
    /// The entity must already exist in the data store.
    /// </param>
    /// <returns>The updated entity.</returns>
    public T Update(T entity)
    {
        var entry = DbSet.Update(entity);
        return entry.Entity;
    }

    /// <summary>
    /// Persists all pending changes to the underlying data store.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token to observe while waiting for the operation to complete.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous save operation.
    /// </returns>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => Context.SaveChangesAsync(cancellationToken);

    private static Dictionary<string, string> GetSortMapping<TResult>() =>
        _dtoSortMappingCache.GetOrAdd(typeof(TResult), _ => MapsterSortResolver.GetSortMapping<T, TResult>());
}
