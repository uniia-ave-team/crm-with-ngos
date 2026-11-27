using System.Text.Json;
using System.Text.Json.Nodes;
using Crm.Infrastructure.Persistence.Consts;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Extensions;

/// <summary>
/// Registers PostgreSQL JSONB and safe-cast database functions on <see cref="ModelBuilder"/>
/// using standard EF Core <c>HasDbFunction</c> mappings (zero manual SQL AST construction).
/// </summary>
public static class ModelBuilderDbFunctionsExtensions
{
    /// <summary>
    /// Registers all custom PostgreSQL database function mappings for EF Core LINQ queries.
    /// </summary>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    /// <returns>The configured <see cref="ModelBuilder"/> for chaining.</returns>
    public static ModelBuilder AddCustomDbFunctions(this ModelBuilder modelBuilder)
    {
        RegisterJsonbExtractPathText(modelBuilder, typeof(JsonObject));
        RegisterJsonbExtractPathText(modelBuilder, typeof(JsonDocument));
        RegisterSyncJsonbKeys(modelBuilder);

        RegisterSafeCast(modelBuilder, nameof(PostgresCastFunctions.SafeCastNumber), PostgresConstants.TryNumericFunction, PostgresConstants.Numeric);
        RegisterSafeCast(modelBuilder, nameof(PostgresCastFunctions.SafeCastBoolean), PostgresConstants.TryBooleanFunction, PostgresConstants.Boolean);
        RegisterSafeCast(modelBuilder, nameof(PostgresCastFunctions.SafeCastDate), PostgresConstants.TryDateFunction, PostgresConstants.Date);
        RegisterSafeCast(modelBuilder, nameof(PostgresCastFunctions.SafeCastDateTime), PostgresConstants.TryTimestampTzFunction, PostgresConstants.TimestampWithTimeZone);

        return modelBuilder;
    }

    private static void RegisterJsonbExtractPathText(ModelBuilder modelBuilder, Type jsonParameterType)
    {
        var method = typeof(JsonbFunctionsExtensions).GetMethod(
            nameof(JsonbFunctionsExtensions.JsonbExtractPathText),
            [jsonParameterType, typeof(string)])
            ?? throw new InvalidOperationException($"{nameof(JsonbFunctionsExtensions.JsonbExtractPathText)}({jsonParameterType.Name}) registration failed.");

        modelBuilder.HasDbFunction(method)
            .HasName(PostgresConstants.JsonbExtractPathTextFunction)
            .HasSchema(null)
            .HasStoreType(PostgresConstants.Text)
            .HasParameter("json", p => p.HasStoreType(PostgresConstants.Jsonb))
            .HasParameter("key", p => p.HasStoreType(PostgresConstants.Text));
    }

    private static void RegisterSyncJsonbKeys(ModelBuilder modelBuilder)
    {
        var method = typeof(JsonbFunctionsExtensions).GetMethod(
            nameof(JsonbFunctionsExtensions.SyncJsonbKeys),
            [typeof(JsonObject), typeof(string[]), typeof(string[])])
            ?? throw new InvalidOperationException($"{nameof(JsonbFunctionsExtensions.SyncJsonbKeys)} registration failed.");

        modelBuilder.HasDbFunction(method)
            .HasName(PostgresConstants.SyncJsonbKeysFunction)
            .HasSchema(null)
            .HasStoreType(PostgresConstants.Jsonb)
            .HasParameter("json", p => p.HasStoreType(PostgresConstants.Jsonb))
            .HasParameter("dropKeys", p => p.HasStoreType(PostgresConstants.TextArray))
            .HasParameter("backfillKeys", p => p.HasStoreType(PostgresConstants.TextArray));
    }

    private static void RegisterSafeCast(
        ModelBuilder modelBuilder,
        string methodName,
        string postgresFunctionName,
        string targetStoreType)
    {
        var method = typeof(PostgresCastFunctions).GetMethod(methodName, [typeof(string)])
            ?? throw new InvalidOperationException($"{methodName} registration failed.");

        modelBuilder.HasDbFunction(method)
            .HasName(postgresFunctionName)
            .HasSchema(null)
            .HasStoreType(targetStoreType)
            .HasParameter("value", p => p.HasStoreType(PostgresConstants.Text));
    }
}
