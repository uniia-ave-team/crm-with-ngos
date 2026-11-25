using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Nodes;
using Crm.Domain.Common;
using Crm.Domain.Extensions;
using Crm.Infrastructure.Persistence.Models;

namespace Crm.Infrastructure.Persistence.Extensions;

/// <summary>
/// Declares EF Core database-function stubs and schema-patch expression builders for PostgreSQL <c>jsonb</c> operations.
/// </summary>
public static class JsonbFunctionsExtensions
{
    private const string ExceptionMessage = "This method is for EF Core LINQ queries only.";

    private static readonly MethodInfo ApplyPatchFn = typeof(JsonbFunctionsExtensions).GetMethod(
        nameof(JsonbApplyPatch),
        [typeof(JsonObject), typeof(string[]), typeof(string[]), typeof(string[]), typeof(string[])])!;

    /// <summary>
    /// Applies a flat JSONB schema patch operation in a single linear SQL translation step.
    /// </summary>
    /// <param name="json">The target JSONB document column.</param>
    /// <param name="dropKeys">Keys to remove from the JSONB document.</param>
    /// <param name="targetKeys">Destination keys to write or backfill.</param>
    /// <param name="sourceKeys">Source keys to copy values from (empty string for new defaults).</param>
    /// <param name="fallbackJson">JSON literal fallbacks when the source value is missing or null.</param>
    /// <returns>The patched JSONB document (SQL translation only).</returns>
    public static JsonObject JsonbApplyPatch(
        JsonObject json,
        string[] dropKeys,
        string[] targetKeys,
        string[] sourceKeys,
        string[] fallbackJson)
        => throw new NotSupportedException(ExceptionMessage);

    /// <summary>
    /// Extracts a scalar text value from a JSONB document at the specified property key.
    /// </summary>
    /// <param name="json">The target JSONB document column.</param>
    /// <param name="key">The top-level JSON property key.</param>
    /// <returns>The extracted text value (SQL translation only).</returns>
    public static string? JsonbExtractPathText(JsonObject json, string key)
        => throw new NotSupportedException(ExceptionMessage);

    /// <summary>
    /// Builds an EF Core compatible update expression from <see cref="SchemaDiffResult"/>.
    /// Returns <see langword="null"/> if no schema changes are present in <paramref name="diff"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type owning the JSONB property.</typeparam>
    /// <param name="propertySelector">An expression selecting the JSONB <see cref="JsonObject"/> property.</param>
    /// <param name="diff">The calculated schema diff.</param>
    /// <returns>An EF Core update expression, or <see langword="null"/> when no patch is required.</returns>
    public static Expression<Func<TEntity, JsonObject>>? ToSchemaUpdateLambda<TEntity>(
        this Expression<Func<TEntity, JsonObject>> propertySelector,
        SchemaDiffResult diff)
    {
        ArgumentNullException.ThrowIfNull(propertySelector);
        ArgumentNullException.ThrowIfNull(diff);

        var patch = BuildSchemaPatch(diff);

        if (patch.DropKeys.Length == 0 && patch.TargetKeys.Length == 0)
        {
            return null;
        }

        var initialBody = propertySelector.Body;

        var patchCall = Expression.Call(
            ApplyPatchFn,
            initialBody,
            Expression.Constant(patch.DropKeys),
            Expression.Constant(patch.TargetKeys),
            Expression.Constant(patch.SourceKeys),
            Expression.Constant(patch.FallbackJson));

        return Expression.Lambda<Func<TEntity, JsonObject>>(patchCall, propertySelector.Parameters);
    }

    /// <summary>
    /// Flattens a <see cref="SchemaDiffResult"/> into parallel SQL patch arrays suitable for <see cref="JsonbApplyPatch"/>.
    /// </summary>
    /// <param name="diff">The calculated schema diff.</param>
    /// <returns>A <see cref="SchemaPatch"/> describing all drops, renames, and required backfills.</returns>
    public static SchemaPatch BuildSchemaPatch(SchemaDiffResult diff)
    {
        var drop = new HashSet<string>(StringComparer.Ordinal);
        var entries = new Dictionary<string, (string Source, string Fallback)>(StringComparer.Ordinal);

        foreach (var f in diff.FieldsToRemove)
        {
            drop.Add(f.Key);
        }

        var renamedFrom = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var r in diff.KeyRenames)
        {
            drop.Add(r.OldKey);
            renamedFrom[r.NewKey] = r.OldKey;
            entries[r.NewKey] = (r.OldKey, string.Empty);
        }

        foreach (var f in diff.FieldsNeedingBackfill)
        {
            string sourceKey = renamedFrom.GetValueOrDefault(f.Key, f.Key);
            entries[f.Key] = (sourceKey, f.DataType.GetBackfillJson());
        }

        foreach (var f in diff.FieldsToAdd)
        {
            if (f.IsRequired)
            {
                entries[f.Key] = (string.Empty, f.DataType.GetBackfillJson());
            }
        }

        return new SchemaPatch(
            [.. drop],
            [.. entries.Keys],
            [.. entries.Values.Select(v => v.Source)],
            [.. entries.Values.Select(v => v.Fallback)]);
    }
}
