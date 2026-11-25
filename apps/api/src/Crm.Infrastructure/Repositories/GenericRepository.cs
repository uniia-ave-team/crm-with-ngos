using System.Collections.Concurrent;
using System.Linq.Expressions;
using Crm.Domain.Constants;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Entities;
using Crm.Domain.Interfaces.Repositories.Generic;
using Crm.Infrastructure.Auditing;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Extensions;
using Crm.Infrastructure.Persistence.Sorting;
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
    private static readonly ConcurrentDictionary<Type, Dictionary<string, string>> DtoSortMappingCache = new();

    /// <summary>
    /// Gets the <see cref="DbSet{TEntity}"/> for the entity type <typeparamref name="T"/>.
    /// </summary>
    protected DbSet<T> DbSet { get; } = context.Set<T>();

    /// <summary>
    /// Gets the <see cref="DbContext"/>.
    /// </summary>
    protected DbContext Context { get; } = context;

    /// <inheritdoc />
    public virtual async Task<T> CreateAsync(T entity, CancellationToken cancellationToken = default)
    {
        var entry = await DbSet.AddAsync(entity, cancellationToken);
        return entry.Entity;
    }

    /// <inheritdoc />
    public virtual Task CreateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        => DbSet.AddRangeAsync(entities, cancellationToken);

    /// <inheritdoc />
    public virtual async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (typeof(ISoftDeletableEntity).IsAssignableFrom(typeof(T)))
        {
            T entity = await DbSet.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new EntityNotFoundException<T>(id);

            DbSet.Remove(entity);
            return;
        }

        var rowsAffected = await DbSet
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        if (rowsAffected == 0)
        {
            throw new EntityNotFoundException<T>(id);
        }

        Context.TryEnqueueBulkDeleteAudit<T>(id.ToString());
    }

    /// <inheritdoc />
    public virtual Task<T> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync(x => x.Id == id, id, cancellationToken);

    /// <inheritdoc />
    public virtual Task<TResult> GetAsync<TResult>(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<TResult>(x => x.Id == id, id, cancellationToken);

    /// <inheritdoc />
    public virtual Task<T> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
        => GetForUpdateAsync(x => x.Id == id, id, cancellationToken);

    /// <inheritdoc />
    public virtual Task EnsureExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => EnsureExistsAsync(m => m.Id == id, id, cancellationToken);

    /// <inheritdoc />
    public virtual Task EnsureAllExistAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        => EnsureAllExistAsync(ids, scopePredicate: null, cancellationToken);

    /// <summary>
    /// Asynchronously acquires a pessimistic row-level write lock (<c>FOR NO KEY UPDATE</c>) on the entity
    /// with the specified identifier within the active transaction without blocking foreign-key child inserts.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to lock.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous lock operation.</returns>
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
    protected virtual Task<Domain.Common.PagedResult<TResult>> GetPagedAsync<TResult>(
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

    /// <summary>
    /// Executes pagination over a filtered and ordered queryable, applying <c>Skip</c>/<c>Take</c>
    /// before Mapster SQL projection so nested subqueries only evaluate on the requested page.
    /// </summary>
    /// <typeparam name="TResult">The target projection DTO type.</typeparam>
    /// <param name="query">The filtered and ordered entity queryable.</param>
    /// <param name="pageNumber">The 1-based page number.</param>
    /// <param name="pageSize">The requested page size.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated result containing the projected page items and total item count.</returns>
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
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ProjectToType<TResult>()
            .ToListAsync(cancellationToken);

        return new(items, totalCount, pageNumber, pageSize);
    }

    /// <summary>
    /// Applies strongly-typed ordering resolved from Mapster DTO-to-entity mappings.
    /// Can be overridden in derived repositories to support custom sorting logic.
    /// </summary>
    /// <typeparam name="TResult">The target DTO or projection type.</typeparam>
    /// <param name="query">The source entity queryable.</param>
    /// <param name="orderBy">The requested DTO property name to sort by.</param>
    /// <param name="sortOrder">The sort direction (<c>asc</c> or <c>desc</c>).</param>
    /// <returns>The ordered entity queryable.</returns>
    protected virtual IQueryable<T> ApplyOrdering<TResult>(
        IQueryable<T> query,
        string orderBy,
        string? sortOrder)
    {
        string normalizedOrderBy = orderBy.Trim();
        var sortMapping = GetSortMapping<TResult>();

        if (sortMapping.TryGetValue(normalizedOrderBy, out string? resolvedEntityPath))
        {
            if (TypedPropertyOrderBuilder.TryApplyOrdering(query, resolvedEntityPath, sortOrder.IsDescending(), out IQueryable<T> orderedQuery))
            {
                return orderedQuery;
            }
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
    /// (<c>ORDER BY Id FOR NO KEY UPDATE</c>) to prevent deadlocks and returns their unique identifiers.
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
        DtoSortMappingCache.GetOrAdd(typeof(TResult), _ => MapsterSortResolver.GetSortMapping<T, TResult>());
}
