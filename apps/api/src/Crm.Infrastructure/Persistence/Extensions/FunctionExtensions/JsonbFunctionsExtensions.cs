using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Nodes;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Extensions;

namespace Crm.Infrastructure.Persistence.Extensions.FunctionExtensions;

public static class JsonbFunctionsExtensions
{
    private const string ExceptionMessage = "This method is for EF Core LINQ queries only.";
    private static readonly MethodInfo RemoveFn = typeof(JsonbFunctionsExtensions).GetMethod(nameof(JsonbRemove), [typeof(JsonObject), typeof(string)])!;
    private static readonly MethodInfo RenameFn = typeof(JsonbFunctionsExtensions).GetMethod(nameof(JsonbRenameKey), [typeof(JsonObject), typeof(string), typeof(string)])!;
    private static readonly MethodInfo ConcatPayloadFn = typeof(JsonbFunctionsExtensions).GetMethod(nameof(JsonbConcatPayload), [typeof(JsonObject), typeof(string)])!;

    /// <summary>
    /// Concatenates a JSON payload string to a PostgreSQL jsonb column using the '||' operator.
    /// </summary>
    public static JsonObject JsonbConcatPayload(JsonObject json, string jsonPayload)
        => throw new NotSupportedException(ExceptionMessage);

    /// <summary>
    /// Extracts string values from a JSONB document into a single text stream for search matching.
    /// Mapped to the PostgreSQL function <c>jsonb_values_to_text</c>.
    /// </summary>
    public static string JsonValuesToText(JsonObject data)
        => throw new NotSupportedException(ExceptionMessage);

    /// <summary>
    /// Removes a key from a PostgreSQL jsonb column using the '-' operator.
    /// </summary>
    public static JsonObject JsonbRemove(JsonObject json, string key)
        => throw new NotSupportedException(ExceptionMessage);

    /// <summary>
    /// Renames a key in a PostgreSQL jsonb column while preserving its associated value.
    /// </summary>
    public static JsonObject JsonbRenameKey(JsonObject json, string oldKey, string newKey)
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

        var initialBody = propertySelector.Body;

        var updatedBody = initialBody
            .ApplyPurge(diff.FieldsToRemove)
            .ApplyRenames(diff.KeyRenames)
            .ApplyBackfill(diff.FieldsToAdd, diff.FieldsNeedingBackfill);

        return updatedBody == initialBody ? null : Expression.Lambda<Func<TEntity, JsonObject>>(updatedBody, propertySelector.Parameters);
    }

    private static Expression ApplyPurge(this Expression body, IEnumerable<CustomField> fieldsToRemove)
    {
        return fieldsToRemove.Aggregate(body, (e, f) =>
            Expression.Call(RemoveFn, e, Expression.Constant(f.Key)));
    }

    private static Expression ApplyRenames(this Expression body, IEnumerable<KeyRename> keyRenames)
    {
        body = keyRenames.Aggregate(body, (e, rn) =>
            Expression.Call(RenameFn, e, Expression.Constant(rn.OldKey), Expression.Constant(rn.TempKey)));

        return keyRenames.Aggregate(body, (e, rn) =>
            Expression.Call(RenameFn, e, Expression.Constant(rn.TempKey), Expression.Constant(rn.NewKey)));
    }

    private static Expression ApplyBackfill(
        this Expression body,
        IEnumerable<CustomField> fieldsToAdd,
        IEnumerable<CustomField> fieldsNeedingBackfill)
    {
        var fieldsToSeed = fieldsToAdd
            .Where(f => f.IsRequired)
            .Concat(fieldsNeedingBackfill)
            .DistinctBy(f => f.Key)
            .ToList();

        if (fieldsToSeed.Count == 0)
        {
            return body;
        }

        body = fieldsToSeed.Aggregate(body, (e, f) =>
            Expression.Call(RemoveFn, e, Expression.Constant(f.Key)));

        var backfillPayload = BuildBackfillPayload(fieldsToSeed);

        return Expression.Call(ConcatPayloadFn, body, Expression.Constant(backfillPayload.ToJsonString()));
    }

    private static JsonObject BuildBackfillPayload(IEnumerable<CustomField> fields)
    {
        var payload = new JsonObject();

        foreach (var field in fields)
        {
            var rawValue = field.DataType.GetBackfillValue();

            payload[field.Key] = rawValue.DeepClone();
        }

        return payload;
    }
}
