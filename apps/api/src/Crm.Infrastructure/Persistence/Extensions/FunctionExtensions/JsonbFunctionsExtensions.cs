using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Nodes;
using Crm.Domain.Common;
using Crm.Domain.Extensions;

namespace Crm.Infrastructure.Persistence.Extensions.FunctionExtensions;

public static class JsonbFunctionsExtensions
{
    private const string ExceptionMessage = "This method is for EF Core LINQ queries only.";

    private static readonly MethodInfo ApplyPatchFn = typeof(JsonbFunctionsExtensions).GetMethod(
        nameof(JsonbApplyPatch),
        [typeof(JsonObject), typeof(string[]), typeof(string[]), typeof(string[]), typeof(string[])])!;

    /// <summary>
    /// Applies a flat JSONB schema patch operation in a single linear SQL translation step.
    /// </summary>
    public static JsonObject JsonbApplyPatch(
        JsonObject json,
        string[] dropKeys,
        string[] targetKeys,
        string[] sourceKeys,
        string[] fallbackJson)
        => throw new NotSupportedException(ExceptionMessage);

    /// <summary>
    /// Builds an EF Core compatible update expression from SchemaDiffResult.
    /// Returns null if no schema changes are present in diff.
    /// </summary>
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
