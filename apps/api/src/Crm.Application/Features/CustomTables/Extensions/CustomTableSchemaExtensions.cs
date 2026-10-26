using Crm.Application.Extensions;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces;

namespace Crm.Application.Features.CustomTables.Extensions;

/// <summary>
/// Contains extension methods for calculating structural schema differences on <see cref="CustomTable"/> entities.
/// </summary>
public static class CustomTableSchemaExtensions
{
    /// <summary>
    /// Computes structural schema differences between the current table field state and an incoming set of field update DTOs.
    /// Utilizes <see cref="FieldSchemaSynchronizer.Reconcile{TEntity, TDto}"/> for collection comparison and identifies key renames,
    /// mandatory field backfills, and forbidden data type modifications.
    /// </summary>
    /// <param name="table">The target custom table entity containing the current field schema.</param>
    /// <param name="incomingFields">The collection of updated field definitions from the request payload.</param>
    /// <param name="hasExistingRecords">Indicates whether the custom table contains existing records in the database.</param>
    /// <returns>A <see cref="SchemaDiffResult"/> instance describing all required field changes, key renames, and record backfill requirements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="table"/> or <paramref name="incomingFields"/> is <see langword="null"/>.</exception>
    /// <exception cref="EntityNotFoundException{CustomField}">Thrown when an incoming field ID does not exist within the table's current field collection.</exception>
    /// <exception cref="FieldDataTypeChangeNotAllowedException">Thrown when attempting to modify a field's data type on a table that already contains records.</exception>
    /// <exception cref="InvalidOperationException">Thrown when duplicate field IDs are present within incoming fields.</exception>
    public static SchemaDiffResult ComputeSchemaDiff(
        this CustomTable table,
        IReadOnlyCollection<IIdentifiableField> incomingFields,
        bool hasExistingRecords)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(incomingFields);

        List<KeyRename> keyRenames = [];
        List<CustomField> needingBackfill = [];

        var result = FieldSchemaSynchronizer.Reconcile(
            table.Fields as IReadOnlyCollection<CustomField> ?? [.. table.Fields],
            incomingFields,
            id => new EntityNotFoundException<CustomField>(id),
            onMatched: (existing, dto) =>
            {
                ValidateDataTypeChange(existing, dto, hasExistingRecords);
                DetectKeyRename(existing, dto, keyRenames);
                DetectBackfillRequirement(existing, dto, needingBackfill);
            });

        return new SchemaDiffResult(result.Removed, result.Added, keyRenames, needingBackfill);
    }

    private static void ValidateDataTypeChange(CustomField existingField, IIdentifiableField dto, bool hasExistingRecords)
    {
        if (hasExistingRecords && existingField.DataType != dto.DataType)
        {
            throw new FieldDataTypeChangeNotAllowedException(existingField.Key);
        }
    }

    private static void DetectKeyRename(CustomField existingField, IIdentifiableField dto, List<KeyRename> keyRenames)
    {
        if (!string.Equals(existingField.Key, dto.Key, StringComparison.Ordinal))
        {
            keyRenames.Add(new KeyRename(existingField.Id, existingField.Key, dto.Key));
        }
    }

    private static void DetectBackfillRequirement(CustomField existingField, IIdentifiableField dto, List<CustomField> needingBackfill)
    {
        if (!existingField.IsRequired && dto.IsRequired)
        {
            needingBackfill.Add(existingField);
        }
    }
}
