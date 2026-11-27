using Crm.Domain.Common;
using Crm.Domain.Entities.Base;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Entities;
using Mapster;

namespace Crm.Application.Extensions;

/// <summary>
/// Synchronizes persisted <see cref="FieldDefinitionBase"/> collections against incoming <see cref="IIdentifiableField"/> DTOs.
/// Existing fields are matched strictly by <see cref="IIdentifiableField.Id"/> and keep their immutable <see cref="BaseEntity.Id"/>;
/// newly added fields receive fresh Version 7 GUID identifiers from <see cref="FieldDefinitionBase"/>.
/// </summary>
public static class FieldSchemaSynchronizer
{
    /// <summary>
    /// Synchronizes <paramref name="existingFields"/> in-place with <paramref name="incomingFields"/>
    /// and returns a <see cref="FieldSchemaSyncPlan{TField}"/> describing added/removed entities and JSONB field-ID keys to drop or backfill.
    /// </summary>
    /// <typeparam name="TEntity">The concrete field entity type (<see cref="Domain.Entities.CustomField"/> or <see cref="Domain.Entities.UserCustomField"/>).</typeparam>
    /// <typeparam name="TDto">The incoming identifiable field DTO type.</typeparam>
    /// <param name="existingFields">The mutable collection of existing field entities to synchronize in-place.</param>
    /// <param name="incomingFields">The desired field definitions from the update request.</param>
    /// <returns>A strongly-typed <see cref="FieldSchemaSyncPlan{TField}"/> ready for persistence and JSONB payload synchronization.</returns>
    /// <exception cref="EntityNotFoundException{TEntity}">Thrown when an incoming field references an unknown identifier.</exception>
    /// <exception cref="DomainValidationException">Thrown when duplicate field identifiers appear in <paramref name="incomingFields"/>.</exception>
    public static FieldSchemaSyncPlan<TEntity> SynchronizeFields<TEntity, TDto>(
        this ICollection<TEntity> existingFields,
        IReadOnlyCollection<TDto> incomingFields)
        where TEntity : FieldDefinitionBase
        where TDto : IIdentifiableField
    {
        ArgumentNullException.ThrowIfNull(existingFields);
        ArgumentNullException.ThrowIfNull(incomingFields);

        var byId = existingFields.ToDictionary(f => f.Id);
        var matchedIds = new HashSet<Guid>(incomingFields.Count);
        var pairs = new List<(TEntity Existing, TDto Dto)>(incomingFields.Count);
        var added = new List<TEntity>();
        var keysToBackfill = new List<string>();

        foreach (var dto in incomingFields)
        {
            if (dto.Id is { } id && id != Guid.Empty)
            {
                if (!byId.TryGetValue(id, out var existing))
                {
                    throw new EntityNotFoundException<TEntity>(id);
                }

                if (!matchedIds.Add(id))
                {
                    throw new DomainValidationException($"Duplicate field ID '{id}' in update request.");
                }

                pairs.Add((existing, dto));
            }
            else
            {
                var created = dto.Adapt<TEntity>();
                added.Add(created);

                if (created.IsRequired)
                {
                    keysToBackfill.Add(created.Id.ToString());
                }
            }
        }

        foreach (var (existing, dto) in pairs)
        {
            bool becameRequired = !existing.IsRequired && dto.IsRequired;

            dto.Adapt(existing);

            if (becameRequired)
            {
                keysToBackfill.Add(existing.Id.ToString());
            }
        }

        var removed = existingFields
            .Where(f => !matchedIds.Contains(f.Id))
            .ToList();

        foreach (var stale in removed)
        {
            existingFields.Remove(stale);
        }

        foreach (var newField in added)
        {
            existingFields.Add(newField);
        }

        string[] keysToDrop = [.. removed.Select(f => f.Id.ToString())];

        return new FieldSchemaSyncPlan<TEntity>(
            added,
            removed,
            keysToDrop,
            [.. keysToBackfill]);
    }
}
