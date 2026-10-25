using Crm.Domain.Entities;

namespace Crm.Domain.Common;

/// <summary>
/// Contains the calculated difference between current schema and updated field definitions.
/// </summary>
/// <param name="FieldsToRemove">The list of fields marked for deletion.</param>
/// <param name="FieldsToAdd">The list of newly created fields to be added.</param>
/// <param name="KeyRenames">The list of key renaming operations that need two-phase execution.</param>
/// <param name="FieldsNeedingBackfill">The list of fields that were made required and need default value backfilling.</param>
public sealed record SchemaDiffResult(
    IReadOnlyList<CustomField> FieldsToRemove,
    IReadOnlyList<CustomField> FieldsToAdd,
    IReadOnlyList<KeyRename> KeyRenames,
    IReadOnlyList<CustomField> FieldsNeedingBackfill);
