using Crm.Application.Dtos.Common;
using Crm.Domain.Common;
using Crm.Domain.Entities.Base;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Entities;
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
    /// Maps matched entities in-place using provided delegates and executes an optional domain inspection delegate prior to mutation.
    /// Automatically pairs DTOs missing an explicit ID by key with unclaimed existing entities.
    /// </summary>
    /// <typeparam name="TEntity">The target domain entity type implementing <see cref="IIdentifiableField"/>.</typeparam>
    /// <typeparam name="TDto">The incoming DTO type implementing <see cref="IIdentifiableField"/>.</typeparam>
    /// <param name="existingItems">The target collection of existing domain field entities.</param>
    /// <param name="incomingDtos">The source sequence of incoming DTOs containing requested schema changes.</param>
    /// <param name="notFoundExceptionFactory">A delegate invoked to construct an exception when an incoming DTO references an entity ID not present in <paramref name="existingItems"/>.</param>
    /// <param name="createEntity">A delegate that converts an incoming DTO into a new domain entity instance.</param>
    /// <param name="updateEntity">A delegate that updates properties of an existing entity from an incoming DTO.</param>
    /// <param name="onMatched">An optional callback executed for each matched entity/DTO pair <c>before</c> properties are updated (e.g., for domain validation or detecting schema mutations).</param>
    /// <returns>A <see cref="ReconciliationResult{TEntity}"/> instance containing categorized lists of removed, added, and matched entities.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="existingItems"/>, <paramref name="incomingDtos"/>, <paramref name="notFoundExceptionFactory"/>, <paramref name="createEntity"/>, or <paramref name="updateEntity"/> is <see langword="null"/>.</exception>
    /// <exception cref="DomainValidationException">Thrown when duplicate field IDs are present in <paramref name="incomingDtos"/>.</exception>
    /// <exception cref="NotFoundException">Thrown via <paramref name="notFoundExceptionFactory"/> when a specified field ID is not found.</exception>
    public static ReconciliationResult<TEntity> Reconcile<TEntity, TDto>(
         IReadOnlyCollection<TEntity> existingItems,
         IEnumerable<TDto> incomingDtos,
         Func<Guid, NotFoundException> notFoundExceptionFactory,
         Func<TDto, TEntity> createEntity,
         Action<TEntity, TDto> updateEntity,
         Action<TEntity, TDto>? onMatched = null)
         where TEntity : class, IIdentifiableField
         where TDto : IIdentifiableField
    {
        ArgumentNullException.ThrowIfNull(existingItems);
        ArgumentNullException.ThrowIfNull(incomingDtos);
        ArgumentNullException.ThrowIfNull(notFoundExceptionFactory);
        ArgumentNullException.ThrowIfNull(createEntity);
        ArgumentNullException.ThrowIfNull(updateEntity);

        var dtos = incomingDtos as IReadOnlyCollection<TDto> ?? [.. incomingDtos];

        var byId = IndexExistingById(existingItems);
        var (claimedIds, hasIdless) = ValidateAndCollectClaimedIds(dtos, byId, notFoundExceptionFactory);
        var unclaimedByKey = BuildUnclaimedByKeyLookup(existingItems, claimedIds, hasIdless);

        var (pairs, added) = PairEntitiesWithDtos(dtos, byId, unclaimedByKey, createEntity);

        ApplyMatchedPairs(pairs, updateEntity, onMatched);

        return BuildResult(existingItems, pairs, added);
    }

    /// <summary>
    /// Synchronizes an existing domain entity collection in-place with incoming DTOs by updating matched entities,
    /// removing stale entities, and appending newly added entities.
    /// </summary>
    /// <typeparam name="TEntity">The target domain entity type implementing <see cref="IIdentifiableField"/>.</typeparam>
    /// <typeparam name="TDto">The incoming DTO type implementing <see cref="IIdentifiableField"/>.</typeparam>
    /// <param name="existingFields">The target mutable collection of existing domain entities to be updated in-place.</param>
    /// <param name="incomingDtos">The source sequence of incoming DTOs.</param>
    /// <param name="notFoundExceptionFactory">A delegate invoked when an incoming DTO references an entity ID not present in <paramref name="existingFields"/>.</param>
    /// <param name="createEntity">A delegate that converts an incoming DTO into a new domain entity instance.</param>
    /// <param name="updateEntity">A delegate that updates properties of an existing entity from an incoming DTO.</param>
    /// <param name="onMatched">An optional callback executed for each matched entity/DTO pair <c>before</c> properties are updated.</param>
    /// <returns>A <see cref="ReconciliationResult{TEntity}"/> instance containing categorized lists of removed, added, and matched entities.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="existingFields"/> or <paramref name="incomingDtos"/> is <see langword="null"/>.</exception>
    public static ReconciliationResult<TEntity> Synchronize<TEntity, TDto>(
        ICollection<TEntity> existingFields,
        IEnumerable<TDto> incomingDtos,
        Func<Guid, NotFoundException> notFoundExceptionFactory,
        Func<TDto, TEntity> createEntity,
        Action<TEntity, TDto> updateEntity,
        Action<TEntity, TDto>? onMatched = null)
        where TEntity : class, IIdentifiableField
        where TDto : IIdentifiableField
    {
        ArgumentNullException.ThrowIfNull(existingFields);

        var existingList = existingFields as IReadOnlyCollection<TEntity> ?? [.. existingFields];
        var result = Reconcile(existingList, incomingDtos, notFoundExceptionFactory, createEntity, updateEntity, onMatched);

        foreach (var stale in result.Removed)
        {
            existingFields.Remove(stale);
        }

        foreach (var newEntity in result.Added)
        {
            existingFields.Add(newEntity);
        }

        return result;
    }

    /// <summary>
    /// Computes structural schema differences between an existing <see cref="FieldDefinitionBase"/> collection and incoming field DTOs,
    /// synchronizing <paramref name="existingFields"/> in-place and detecting key renames, mandatory backfills, and disallowed data type changes.
    /// </summary>
    /// <typeparam name="TEntity">The concrete field definition entity type.</typeparam>
    /// <typeparam name="TDto">The incoming identifiable field DTO type.</typeparam>
    /// <param name="existingFields">The mutable collection of existing field entities to synchronize in-place.</param>
    /// <param name="incomingFields">The collection of incoming field definitions.</param>
    /// <param name="isDataTypeChangeBlocked">Predicate returning <see langword="true"/> when a matched entity's data type cannot be changed.</param>
    /// <returns>A <see cref="SchemaDiffResult"/> describing all additions, removals, key renames, and required backfills.</returns>
    public static SchemaDiffResult ComputeSchemaDiff<TEntity, TDto>(
        this ICollection<TEntity> existingFields,
        IReadOnlyCollection<TDto> incomingFields,
        Func<TEntity, bool> isDataTypeChangeBlocked)
        where TEntity : FieldDefinitionBase
        where TDto : IIdentifiableField
    {
        ArgumentNullException.ThrowIfNull(existingFields);
        ArgumentNullException.ThrowIfNull(incomingFields);
        ArgumentNullException.ThrowIfNull(isDataTypeChangeBlocked);

        List<KeyRename> keyRenames = [];
        List<FieldDefinitionBase> needingBackfill = [];

        var result = Synchronize(
            existingFields,
            incomingFields,
            id => new EntityNotFoundException<TEntity>(id),
            createEntity: dto => dto.Adapt<TEntity>(),
            updateEntity: (existing, dto) => dto.Adapt(existing),
            onMatched: (existing, dto) =>
            {
                if (existing.DataType != dto.DataType && isDataTypeChangeBlocked(existing))
                {
                    throw new FieldDataTypeChangeNotAllowedException(existing.Key);
                }

                if (!string.Equals(existing.Key, dto.Key, StringComparison.Ordinal))
                {
                    keyRenames.Add(new KeyRename(existing.Id, existing.Key, dto.Key));
                }

                if (!existing.IsRequired && dto.IsRequired)
                {
                    needingBackfill.Add(existing);
                }
            });

        return new SchemaDiffResult(result.Removed, result.Added, keyRenames, needingBackfill);
    }

    /// <summary>
    /// Returns the existing keys of fields whose <see cref="IFieldSchema.DataType"/> is being changed by <paramref name="incomingFields"/>.
    /// </summary>
    /// <typeparam name="TEntity">The concrete field definition entity type.</typeparam>
    /// <typeparam name="TDto">The incoming identifiable field DTO type.</typeparam>
    /// <param name="existingFields">The current field definitions.</param>
    /// <param name="incomingFields">The incoming field definitions.</param>
    /// <returns>A list of existing field keys whose data types differ from their matched incoming DTOs.</returns>
    public static List<string> FindKeysWithDataTypeChange<TEntity, TDto>(
        this IReadOnlyCollection<TEntity> existingFields,
        IReadOnlyCollection<TDto> incomingFields)
        where TEntity : FieldDefinitionBase
        where TDto : IIdentifiableField
    {
        ArgumentNullException.ThrowIfNull(existingFields);
        ArgumentNullException.ThrowIfNull(incomingFields);

        if (existingFields.Count == 0 || incomingFields.Count == 0)
        {
            return [];
        }

        var existingById = existingFields.ToDictionary(f => f.Id);
        List<string> keys = [];

        foreach (var dto in incomingFields)
        {
            if (dto.Id is { } id
                && existingById.TryGetValue(id, out var existing)
                && existing.DataType != dto.DataType)
            {
                keys.Add(existing.Key);
            }
        }

        return keys;
    }

    private static Dictionary<Guid, TEntity> IndexExistingById<TEntity>(IReadOnlyCollection<TEntity> existingItems)
        where TEntity : class, IIdentifiableField
    {
        var byId = new Dictionary<Guid, TEntity>(existingItems.Count);
        foreach (var e in existingItems)
        {
            if (e.Id is { } eid && eid != Guid.Empty)
            {
                byId[eid] = e;
            }
        }

        return byId;
    }

    private static (HashSet<Guid> Claimed, bool HasIdless) ValidateAndCollectClaimedIds<TEntity, TDto>(
        IReadOnlyCollection<TDto> dtos,
        Dictionary<Guid, TEntity> byId,
        Func<Guid, NotFoundException> notFoundExceptionFactory)
        where TEntity : class, IIdentifiableField
        where TDto : IIdentifiableField
    {
        var claimed = new HashSet<Guid>(dtos.Count);
        var hasIdless = false;

        foreach (var dto in dtos)
        {
            if (dto.Id is not { } id || id == Guid.Empty)
            {
                hasIdless = true;
                continue;
            }

            if (!byId.ContainsKey(id))
            {
                throw notFoundExceptionFactory(id);
            }

            if (!claimed.Add(id))
            {
                throw new DomainValidationException($"Duplicate field ID '{id}' in update request.");
            }
        }

        return (claimed, hasIdless);
    }

    private static Dictionary<string, TEntity>? BuildUnclaimedByKeyLookup<TEntity>(
        IReadOnlyCollection<TEntity> existingItems,
        HashSet<Guid> claimed,
        bool hasIdless)
        where TEntity : class, IIdentifiableField
    {
        if (!hasIdless)
        {
            return null;
        }

        var unclaimedByKey = new Dictionary<string, TEntity>(existingItems.Count, StringComparer.Ordinal);
        foreach (var e in existingItems)
        {
            if (!(e.Id is { } eid && claimed.Contains(eid)))
            {
                unclaimedByKey.TryAdd(e.Key, e);
            }
        }

        return unclaimedByKey;
    }

    private static (List<(TEntity Target, TDto Dto)> Pairs, List<TEntity> Added) PairEntitiesWithDtos<TEntity, TDto>(
        IReadOnlyCollection<TDto> dtos,
        Dictionary<Guid, TEntity> byId,
        Dictionary<string, TEntity>? unclaimedByKey,
        Func<TDto, TEntity> createEntity)
        where TEntity : class, IIdentifiableField
        where TDto : IIdentifiableField
    {
        var pairs = new List<(TEntity Target, TDto Dto)>(dtos.Count);
        var added = new List<TEntity>();

        foreach (var dto in dtos)
        {
            TEntity? target = null;

            if (dto.Id is { } id && id != Guid.Empty)
            {
                target = byId[id];
            }
            else if (unclaimedByKey is not null
                && unclaimedByKey.TryGetValue(dto.Key, out var hit)
                && hit.DataType == dto.DataType)
            {
                unclaimedByKey.Remove(dto.Key);
                target = hit;
            }

            if (target is null)
            {
                added.Add(createEntity(dto));
            }
            else
            {
                pairs.Add((target, dto));
            }
        }

        return (pairs, added);
    }

    private static void ApplyMatchedPairs<TEntity, TDto>(
        List<(TEntity Target, TDto Dto)> pairs,
        Action<TEntity, TDto> updateEntity,
        Action<TEntity, TDto>? onMatched)
        where TEntity : class, IIdentifiableField
        where TDto : IIdentifiableField
    {
        // Phase 1: Validation
        if (onMatched is not null)
        {
            foreach (var (t, d) in pairs)
            {
                onMatched(t, d);
            }
        }

        // Phase 2: Mutation
        foreach (var (t, d) in pairs)
        {
            updateEntity(t, d);
        }
    }

    private static ReconciliationResult<TEntity> BuildResult<TEntity, TDto>(
        IReadOnlyCollection<TEntity> existingItems,
        List<(TEntity Target, TDto Dto)> pairs,
        List<TEntity> added)
        where TEntity : class, IIdentifiableField
    {
        var matchedSet = new HashSet<TEntity>(pairs.Count, ReferenceEqualityComparer.Instance);
        var matched = new List<TEntity>(pairs.Count);
        foreach (var (t, _) in pairs)
        {
            matched.Add(t);
            matchedSet.Add(t);
        }

        var removed = existingItems
            .Where(e => !matchedSet.Contains(e))
            .ToList();

        return new ReconciliationResult<TEntity>(removed, added, matched);
    }
}
