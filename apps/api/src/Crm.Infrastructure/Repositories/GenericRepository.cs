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
    /// Case-insensitive whitelist of allowed sort direction specifiers (<see cref="SortOrderConstants.Ascending"/> / <see cref="SortOrderConstants.Descending"/>) for dynamic sorting validation.
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
    public virtual async Task<T> CreateAsync(T entity, CancellationToken cancellationToken = default)
    {
        var entry = await DbSet.AddAsync(entity, cancellationToken);
        return entry.Entity;
    }

    /// <summary>
    /// Asynchronously adds a collection of entities to the database context.
    /// </summary>
    /// <param name="entities">The collection of entities to create.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public virtual Task CreateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        => DbSet.AddRangeAsync(entities, cancellationToken);

    /// <summary>
    /// Asynchronously deletes an entity by its unique identifier using a bulk delete operation.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="NotFoundException">Thrown if the entity with the specified ID is not found.</exception>
    public virtual async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await DbSet
                    .Where(x => x.Id == id)
                    .ExecuteDeleteAsync(cancellationToken);

        if (rowsAffected == 0)
        {
            throw new EntityNotFoundException<T>(id);
        }
    }

    /// <summary>
    /// Asynchronously retrieves an entity by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The entity with the specified ID.</returns>
    /// <exception cref="NotFoundException">Thrown if the entity with the specified ID is not found.</exception>
    public virtual Task<T> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync(x => x.Id == id, id, cancellationToken);

    /// <summary>
    /// Asynchronously retrieves and projects an entity by its unique identifier using Mapster.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected element (e.g., DTO).</typeparam>
    /// <param name="id">The unique identifier of the entity to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The projected entity DTO with the specified ID.</returns>
    /// <exception cref="NotFoundException">Thrown if the entity with the specified ID is not found.</exception>
    public virtual Task<TResult> GetAsync<TResult>(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<TResult>(x => x.Id == id, id, cancellationToken);

    /// <summary>
    /// Asynchronously retrieves an entity by its unique identifier with change tracking enabled.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The tracked entity with the specified ID.</returns>
    /// <exception cref="NotFoundException">Thrown if the entity with the specified ID is not found.</exception>
    public virtual Task<T> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
        => GetForUpdateAsync(x => x.Id == id, id, cancellationToken);

    /// <summary>
    /// Asynchronously checks if an entity with the specified unique identifier exists.
    /// Throws an exception if the entity is not found.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to check.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="NotFoundException">Thrown if no entity with the specified ID exists.</exception>
    public virtual Task EnsureExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => EnsureExistsAsync(m => m.Id == id, id, cancellationToken);

    /// <summary>
    /// Asynchronously ensures that entities exist for all specified unique identifiers.
    /// Throws an exception containing all missing keys if any entity is not found.
    /// </summary>
    /// <param name="ids">The collection of unique identifiers to check.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="EntitiesNotFoundException">Thrown if any entity with the specified ID is not found.</exception>
    public virtual Task EnsureAllExistAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        => EnsureAllExistAsync(ids, scopePredicate: null, cancellationToken);

    /// <inheritdoc />
    public virtual async Task LockForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!Context.Database.IsRelational())
        {
            return;
        }

        _ = await DbSet
            .AsNoTracking()
            .Where(e => e.Id == id)
            .WithRowLock()
            .Select(e => e.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously retrieves all entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of all entities.</returns>
    public virtual async Task<List<T>> GetListAsync(CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking().ToListAsync(cancellationToken);

    /// <summary>
    /// Asynchronously retrieves a single random entity from the database and projects it to the specified type using Mapster.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected element (e.g., DTO).</typeparam>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A random projected entity.</returns>
    /// <exception cref="NotFoundException">Thrown if no entities exist in the database for this type.</exception>
    public virtual async Task<TResult> GetRandomAsync<TResult>(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .OrderBy(x => Guid.NewGuid())
            .ProjectToType<TResult>()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException<T>();
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
    /// <param name="sortOrder">The sort direction (<see cref="SortOrderConstants.Ascending"/> or <see cref="SortOrderConstants.Descending"/>). Defaults to <see cref="SortOrderConstants.Ascending"/>.</param>
    /// <param name="pageNumber">The current page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated result containing the projected items and metadata.</returns>
    public virtual Task<Domain.Common.PagedResult<TResult>> GetPagedAsync<TResult>(
        Expression<Func<T, bool>>? predicate = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = DbSet.AsNoTracking();

        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        query = string.IsNullOrWhiteSpace(orderBy)
            ? query.OrderBy(e => e.Id)
            : ApplyOrdering<TResult>(query, orderBy, sortOrder);

        return ToPagedResultAsync<TResult>(query, pageNumber, pageSize, cancellationToken);
    }

    protected virtual async Task<Domain.Common.PagedResult<TResult>> ToPagedResultAsync<TResult>(
        IQueryable<T> query,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, PaginationConstants.MinPageSize, PaginationConstants.MaxPageSize);
        pageNumber = Math.Max(PaginationConstants.MinPageNumber, pageNumber);

        int totalCount = await query.CountAsync(cancellationToken);
        int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        if (totalCount == 0 || pageNumber > totalPages)
        {
            return new([], totalCount, pageNumber, pageSize);
        }

        var items = await query
            .ProjectToType<TResult>()
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new(items, totalCount, pageNumber, pageSize);
    }

    /// <summary>
    /// Applies dynamic ordering to the query. Can be overridden in derived repositories
    /// to support custom sorting logic.
    /// </summary>
    protected virtual IQueryable<T> ApplyOrdering<TResult>(
        IQueryable<T> query,
        string orderBy,
        string? sortOrder)
    {
        var sortMapping = GetSortMapping<TResult>();

        if (sortMapping.TryGetValue(orderBy, out string? resolvedEntityPath)
            && resolvedEntityPath.All(c => char.IsLetterOrDigit(c) || c == '.'))
        {
            string safeSortOrder = _allowedSortOrders.Contains(sortOrder ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                ? sortOrder
                : SortOrderConstants.Ascending;

            return query.OrderBy($"{resolvedEntityPath} {safeSortOrder}")
                .ThenBy(e => e.Id);
        }

        return query.OrderBy(e => e.Id);
    }

    /// <summary>
    /// Asynchronously retrieves a single entity matching <paramref name="predicate"/> without change tracking.
    /// </summary>
    /// <param name="predicate">A SQL-translatable filter expression matching the entity.</param>
    /// <param name="entityId">The target entity identifier included in <see cref="EntityNotFoundException{T}"/> if not found.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The matching entity.</returns>
    /// <exception cref="EntityNotFoundException{T}">Thrown if no matching entity exists.</exception>
    protected async Task<T> GetAsync(
        Expression<Func<T, bool>> predicate,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(predicate, cancellationToken)
            ?? throw new EntityNotFoundException<T>(entityId);
    }

    /// <summary>
    /// Asynchronously retrieves and projects a single entity matching <paramref name="predicate"/> using Mapster.
    /// </summary>
    /// <typeparam name="TResult">The target DTO or projection type.</typeparam>
    /// <param name="predicate">A SQL-translatable filter expression matching the entity.</param>
    /// <param name="entityId">The target entity identifier included in <see cref="EntityNotFoundException{T}"/> if not found.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The projected entity DTO.</returns>
    /// <exception cref="EntityNotFoundException{T}">Thrown if no matching entity exists.</exception>
    protected async Task<TResult> GetAsync<TResult>(
        Expression<Func<T, bool>> predicate,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(predicate)
            .ProjectToType<TResult>()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException<T>(entityId);
    }

    /// <summary>
    /// Asynchronously retrieves a single tracked entity matching <paramref name="predicate"/> for updates.
    /// </summary>
    /// <param name="predicate">A SQL-translatable filter expression matching the entity.</param>
    /// <param name="entityId">The target entity identifier included in <see cref="EntityNotFoundException{T}"/> if not found.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The tracked entity.</returns>
    /// <exception cref="EntityNotFoundException{T}">Thrown if no matching entity exists.</exception>
    protected async Task<T> GetForUpdateAsync(
        Expression<Func<T, bool>> predicate,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(predicate, cancellationToken)
            ?? throw new EntityNotFoundException<T>(entityId);
    }

    /// <summary>
    /// Asynchronously locks all rows matching <paramref name="predicate"/> in deterministic identifier order
    /// (<c>ORDER BY Id FOR UPDATE</c>) to prevent deadlocks and returns their unique identifiers.
    /// </summary>
    /// <param name="predicate">A SQL-translatable filter expression selecting the rows to lock.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The ordered list of locked entity identifiers.</returns>
    protected async Task<List<Guid>> LockIdsForUpdateAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = DbSet
            .AsNoTracking()
            .Where(predicate)
            .OrderBy(e => e.Id);

        if (Context.Database.IsRelational())
        {
            query = query.WithRowLock();
        }

        return await query
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously ensures that at least one entity matching <paramref name="predicate"/> exists.
    /// </summary>
    /// <param name="predicate">A SQL-translatable filter expression matching the entity.</param>
    /// <param name="entityId">The target entity identifier included in <see cref="EntityNotFoundException{T}"/> if not found.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous existence check.</returns>
    /// <exception cref="EntityNotFoundException{T}">Thrown if no matching entity exists.</exception>
    protected async Task EnsureExistsAsync(
        Expression<Func<T, bool>> predicate,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        bool exists = await DbSet
            .AsNoTracking()
            .AnyAsync(predicate, cancellationToken);

        if (!exists)
        {
            throw new EntityNotFoundException<T>(entityId);
        }
    }

    /// <summary>
    /// Asynchronously ensures that entities exist for all specified <paramref name="ids"/>,
    /// optionally constrained by <paramref name="scopePredicate"/>.
    /// </summary>
    /// <param name="ids">The collection of entity identifiers to verify.</param>
    /// <param name="scopePredicate">An optional SQL-translatable scope filter (for example, project ownership).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous verification.</returns>
    /// <exception cref="EntitiesNotFoundException">Thrown if any requested identifier is missing within the scope.</exception>
    protected async Task EnsureAllExistAsync(
        IEnumerable<Guid> ids,
        Expression<Func<T, bool>>? scopePredicate,
        CancellationToken cancellationToken = default)
    {
        List<Guid> distinctIds = [.. ids.Distinct()];
        if (distinctIds.Count == 0)
        {
            return;
        }

        IQueryable<T> query = DbSet.AsNoTracking();
        if (scopePredicate is not null)
        {
            query = query.Where(scopePredicate);
        }

        List<Guid> existingIds = await query
            .Where(e => distinctIds.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        List<object> missingIds = [.. distinctIds.Except(existingIds).Cast<object>()];
        if (missingIds.Count > 0)
        {
            throw new EntitiesNotFoundException(typeof(T).Name, missingIds);
        }
    }

    /// <summary>
    /// Stages a join entity record if no record matching <paramref name="matchPredicate"/> currently exists.
    /// </summary>
    /// <typeparam name="TJoin">The explicit many-to-many join entity type.</typeparam>
    /// <param name="matchPredicate">A predicate identifying an existing join record.</param>
    /// <param name="entity">The join entity instance to stage.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> if the join record was newly staged; <c>false</c> if it already existed.</returns>
    protected async Task<bool> AddJoinIfNotExistsAsync<TJoin>(
        Expression<Func<TJoin, bool>> matchPredicate,
        TJoin entity,
        CancellationToken cancellationToken = default)
        where TJoin : class
    {
        if (await Context.Set<TJoin>().AsNoTracking().AnyAsync(matchPredicate, cancellationToken))
        {
            return false;
        }

        await Context.Set<TJoin>().AddAsync(entity, cancellationToken);
        return true;
    }

    /// <summary>
    /// Stages join entity records for a distinct collection of target identifiers.
    /// </summary>
    /// <typeparam name="TJoin">The explicit many-to-many join entity type.</typeparam>
    /// <param name="targetIds">The target entity identifiers to link.</param>
    /// <param name="factory">A factory mapping each target identifier to a join entity instance.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous staging operation.</returns>
    protected Task AddJoinRangeAsync<TJoin>(
        IEnumerable<Guid> targetIds,
        Func<Guid, TJoin> factory,
        CancellationToken cancellationToken = default)
        where TJoin : class
    {
        List<Guid> distinctIds = [.. targetIds.Distinct()];
        return distinctIds.Count == 0
            ? Task.CompletedTask
            : Context.Set<TJoin>().AddRangeAsync(distinctIds.Select(factory), cancellationToken);
    }

    /// <summary>
    /// Atomically removes an existing join entity record matching the predicate if present.
    /// </summary>
    /// <typeparam name="TJoin">The explicit many-to-many join entity type.</typeparam>
    /// <param name="matchPredicate">A predicate identifying the link to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> if the link existed and was deleted; otherwise, <c>false</c>.</returns>
    protected async Task<bool> RemoveJoinAsync<TJoin>(
        Expression<Func<TJoin, bool>> matchPredicate,
        CancellationToken cancellationToken = default)
        where TJoin : class
    {
        int rowsAffected = await Context.Set<TJoin>()
            .Where(matchPredicate)
            .ExecuteDeleteAsync(cancellationToken);

        return rowsAffected > 0;
    }

    /// <summary>
    /// Synchronizes an owner entity's many-to-many join records to match a target set of identifiers.
    /// </summary>
    /// <typeparam name="TJoin">The explicit many-to-many join entity type.</typeparam>
    /// <param name="filter">A predicate selecting all existing join rows belonging to the owner entity.</param>
    /// <param name="targetKeySelector">Extracts the target entity identifier from a join row.</param>
    /// <param name="targetIds">The desired collection of target entity identifiers.</param>
    /// <param name="factory">A factory creating a join entity row for each newly added target identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous synchronization operation.</returns>
    protected async Task SyncJoinAsync<TJoin>(
        Expression<Func<TJoin, bool>> filter,
        Func<TJoin, Guid> targetKeySelector,
        IEnumerable<Guid> targetIds,
        Func<Guid, TJoin> factory,
        CancellationToken cancellationToken = default)
        where TJoin : class
    {
        HashSet<Guid> desired = [.. targetIds];
        var joinSet = Context.Set<TJoin>();
        var existing = await joinSet.Where(filter).ToListAsync(cancellationToken);

        List<TJoin> toRemove = [.. existing.Where(e => !desired.Contains(targetKeySelector(e)))];
        if (toRemove.Count > 0)
        {
            joinSet.RemoveRange(toRemove);
        }

        HashSet<Guid> existingIds = [.. existing.Select(targetKeySelector)];
        List<TJoin> toAdd = [.. desired.Where(id => !existingIds.Contains(id)).Select(factory)];
        if (toAdd.Count > 0)
        {
            await joinSet.AddRangeAsync(toAdd, cancellationToken);
        }
    }

    private static Dictionary<string, string> GetSortMapping<TResult>() =>
        _dtoSortMappingCache.GetOrAdd(typeof(TResult), _ => MapsterSortResolver.GetSortMapping<T, TResult>());
}
