using Crm.Application.Dtos.Common;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces;
using Mapster;

namespace Crm.Application.Extensions;

/// <summary>
/// Provides generic helper methods for reconciling and synchronizing domain field entity collections
/// with incoming data transfer objects (DTOs).
/// </summary>
public static class FieldSchemaSynchronizer
{
    /// <summary>
    /// Performs generic schema reconciliation between an existing entity collection and incoming DTOs.
    /// Maps matched entities in-place using Mapster and executes an optional domain inspection delegate prior to mutation.
    /// </summary>
    /// <typeparam name="TEntity">The target domain entity type implementing <see cref="IIdentifiableField"/>.</typeparam>
    /// <typeparam name="TDto">The incoming DTO type implementing <see cref="IIdentifiableField"/>.</typeparam>
    /// <param name="existingItems">The target collection of existing domain field entities.</param>
    /// <param name="incomingDtos">The source sequence of incoming DTOs containing requested schema changes.</param>
    /// <param name="notFoundExceptionFactory">A delegate invoked to construct an exception when an incoming DTO references an entity ID not present in <paramref name="existingItems"/>.</param>
    /// <param name="onMatched">An optional callback executed for each matched entity/DTO pair <c>before</c> properties are adapted (e.g., for domain validation or detecting schema mutations).</param>
    /// <returns>A <see cref="ReconciliationResult{TEntity}"/> instance containing categorized lists of removed, added, and matched entities.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="existingItems"/> or <paramref name="incomingDtos"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when duplicate field IDs are present in <paramref name="incomingDtos"/>.</exception>
    /// <exception cref="Exception">Thrown via <paramref name="notFoundExceptionFactory"/> when a specified field ID is not found.</exception>
    public static ReconciliationResult<TEntity> Reconcile<TEntity, TDto>(
        IReadOnlyCollection<TEntity> existingItems,
        IEnumerable<TDto> incomingDtos,
        Func<Guid, NotFoundException> notFoundExceptionFactory,
        Action<TEntity, TDto>? onMatched = null)
        where TEntity : class, IIdentifiableField
        where TDto : IIdentifiableField
    {
        ArgumentNullException.ThrowIfNull(existingItems);
        ArgumentNullException.ThrowIfNull(incomingDtos);

        var existingMap = new Dictionary<Guid, TEntity>(existingItems.Count);
        foreach (var item in existingItems)
        {
            if (item.Id.HasValue)
            {
                existingMap[item.Id.Value] = item;
            }
        }

        var keptIds = new HashSet<Guid>();
        var added = new List<TEntity>();
        var matched = new List<TEntity>();

        foreach (var dto in incomingDtos)
        {
            if (dto.Id is not { } id || id == Guid.Empty)
            {
                added.Add(dto.Adapt<TEntity>());
                continue;
            }

            if (!existingMap.TryGetValue(id, out var existing))
            {
                throw notFoundExceptionFactory(id);
            }

            if (!keptIds.Add(id))
            {
                throw new InvalidOperationException($"Duplicate field ID '{id}' in update request.");
            }

            onMatched?.Invoke(existing, dto);

            dto.Adapt(existing);
            matched.Add(existing);
        }

        var removed = new List<TEntity>();
        foreach (var (id, entity) in existingMap)
        {
            if (!keptIds.Contains(id))
            {
                removed.Add(entity);
            }
        }

        return new ReconciliationResult<TEntity>(removed, added, matched);
    }

    /// <summary>
    /// Synchronizes an existing domain entity collection in-place with incoming DTOs by adapting matched entities,
    /// removing stale entities, and appending newly added entities.
    /// </summary>
    /// <typeparam name="TEntity">The target domain entity type implementing <see cref="IIdentifiableField"/>.</typeparam>
    /// <typeparam name="TDto">The incoming DTO type implementing <see cref="IIdentifiableField"/>.</typeparam>
    /// <param name="existingFields">The target mutable collection of existing domain entities to be updated in-place.</param>
    /// <param name="incomingDtos">The source sequence of incoming DTOs.</param>
    /// <param name="notFoundExceptionFactory">A delegate invoked when an incoming DTO references an entity ID not present in <paramref name="existingFields"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="existingFields"/> or <paramref name="incomingDtos"/> is <see langword="null"/>.</exception>
    public static void Synchronize<TEntity, TDto>(
        ICollection<TEntity> existingFields,
        IEnumerable<TDto> incomingDtos,
        Func<Guid, NotFoundException> notFoundExceptionFactory)
        where TEntity : class, IIdentifiableField
        where TDto : IIdentifiableField
    {
        ArgumentNullException.ThrowIfNull(existingFields);

        var existingList = existingFields as IReadOnlyCollection<TEntity> ?? [.. existingFields];
        var result = Reconcile(existingList, incomingDtos, notFoundExceptionFactory);

        foreach (var stale in result.Removed)
        {
            existingFields.Remove(stale);
        }

        foreach (var newEntity in result.Added)
        {
            existingFields.Add(newEntity);
        }
    }
}
