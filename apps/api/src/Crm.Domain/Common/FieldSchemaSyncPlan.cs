using Crm.Domain.Entities.Base;

namespace Crm.Domain.Common;

/// <summary>
/// Captures strongly-typed structural field schema changes (additions and removals) together with the
/// JSONB field-ID keys to drop or backfill when synchronizing a <typeparamref name="TField"/> collection.
/// </summary>
/// <typeparam name="TField">The concrete field entity type inheriting <see cref="FieldDefinitionBase"/>.</typeparam>
/// <param name="AddedFields">Newly created field definitions to persist.</param>
/// <param name="RemovedFields">Existing field definitions removed from the schema.</param>
/// <param name="KeysToDrop">JSONB keys corresponding to removed fields that should be stripped from stored records.</param>
/// <param name="KeysToBackfill">JSONB keys for newly required fields that should be backfilled with empty strings when missing or null.</param>
public sealed record FieldSchemaSyncPlan<TField>(
    IReadOnlyList<TField> AddedFields,
    IReadOnlyList<TField> RemovedFields,
    string[] KeysToDrop,
    string[] KeysToBackfill) : SchemaSyncPlan(KeysToDrop, KeysToBackfill)
    where TField : FieldDefinitionBase;
