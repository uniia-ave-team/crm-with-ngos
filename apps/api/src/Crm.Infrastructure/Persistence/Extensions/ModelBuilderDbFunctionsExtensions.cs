using System.Data;
using System.Linq.Expressions;
using System.Text.Json.Nodes;
using Crm.Infrastructure.Persistence.Extensions.FunctionExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Crm.Infrastructure.Persistence.Extensions;

public static class ModelBuilderDbFunctionsExtensions
{
    private const string JsonbStoreType = "jsonb";
    private const string TextStoreType = "text";
    private const string JsonbExtractPathFunction = "jsonb_extract_path";
    private const string JsonbBuildObjectFunction = "jsonb_build_object";

    private static readonly RelationalTypeMapping JsonbMapping = new StringTypeMapping("jsonb", DbType.String);
    private static readonly RelationalTypeMapping TextMapping = new StringTypeMapping("text", DbType.String);

    /// <summary>
    /// Registers custom database function translations for EF Core.
    /// </summary>
    /// <param name="modelBuilder">The <see cref="ModelBuilder"/> instance.</param>
    /// <returns>The updated <see cref="ModelBuilder"/> instance for chaining.</returns>
    public static ModelBuilder AddCustomDbFunctions(this ModelBuilder modelBuilder)
    {
        RegisterJsonbRemove(modelBuilder);
        RegisterJsonbConcatPayload(modelBuilder);
        RegisterJsonbRenameKey(modelBuilder);
        RegisterJsonValuesToText(modelBuilder);

        return modelBuilder;
    }

    // TODO: Think about adding GIN index for jsonb columns in migrations.
    private static void RegisterJsonValuesToText(ModelBuilder modelBuilder)
    {
        var method = typeof(JsonbFunctionsExtensions)
            .GetMethod(
                nameof(JsonbFunctionsExtensions.JsonValuesToText),
                [typeof(JsonObject)])
            ?? throw new InvalidOperationException("JsonValuesToText registration failed.");

        modelBuilder.HasDbFunction(method)
            .HasStoreType(TextStoreType)
            .HasParameter("data", p => p.HasStoreType(JsonbStoreType))
            .HasTranslation(args =>
            {
                var json = args[0];
                var typedJson = EnsureTypeMapping(json, JsonbMapping);

                return new SqlUnaryExpression(
                    operatorType: ExpressionType.Convert,
                    operand: typedJson,
                    type: typeof(string),
                    typeMapping: TextMapping);
            });
    }

    private static void RegisterJsonbConcatPayload(ModelBuilder modelBuilder)
    {
        var method = typeof(JsonbFunctionsExtensions)
            .GetMethod(nameof(JsonbFunctionsExtensions.JsonbConcatPayload), [typeof(JsonObject), typeof(string)])
            ?? throw new InvalidOperationException("JsonbConcatPayload registration failed.");

        modelBuilder.HasDbFunction(method)
            .HasStoreType(JsonbStoreType)
            .HasParameter("json", p => p.HasStoreType(JsonbStoreType))
            .HasParameter("jsonPayload", p => p.HasStoreType(JsonbStoreType))
            .HasTranslation(args =>
            {
                var json = args[0];
                var payload = args[1];

                var jsonMapping = json.TypeMapping ?? JsonbMapping;

                var typedJson = EnsureTypeMapping(json, jsonMapping);
                var typedPayload = EnsureTypeMapping(payload, jsonMapping);

                return JsonbConcat(typedJson, typedPayload, jsonMapping);
            });
    }

    private static void RegisterJsonbRemove(ModelBuilder modelBuilder)
    {
        var jsonbRemoveMethod = typeof(JsonbFunctionsExtensions)
            .GetMethod(
                nameof(JsonbFunctionsExtensions.JsonbRemove),
                [typeof(JsonObject), typeof(string)])
            ?? throw new InvalidOperationException("JsonbRemove registration failed.");

        modelBuilder.HasDbFunction(jsonbRemoveMethod)
            .HasStoreType(JsonbStoreType)
            .HasParameter("json", p => p.HasStoreType(JsonbStoreType))
            .HasParameter("key", p => p.HasStoreType(TextStoreType))
            .HasTranslation(args =>
            {
                var json = args[0];
                var key = args[1];

                var jsonMapping = json.TypeMapping ?? JsonbMapping;
                var stringMapping = key.TypeMapping ?? TextMapping;

                var typedJson = EnsureTypeMapping(json, jsonMapping);
                var typedKey = EnsureTypeMapping(key, stringMapping);

                return new SqlBinaryExpression(
                    ExpressionType.Subtract,
                    typedJson,
                    typedKey,
                    json.Type,
                    jsonMapping);
            });
    }

    private static void RegisterJsonbRenameKey(ModelBuilder modelBuilder)
    {
        var jsonbRenameMethod = typeof(JsonbFunctionsExtensions)
            .GetMethod(
                nameof(JsonbFunctionsExtensions.JsonbRenameKey),
                [typeof(JsonObject), typeof(string), typeof(string)])
            ?? throw new InvalidOperationException("JsonbRenameKey registration failed.");

        modelBuilder.HasDbFunction(jsonbRenameMethod)
            .HasStoreType(JsonbStoreType)
            .HasParameter("json", p => p.HasStoreType(JsonbStoreType))
            .HasParameter("oldKey", p => p.HasStoreType(TextStoreType))
            .HasParameter("newKey", p => p.HasStoreType(TextStoreType))
            .HasTranslation(args =>
            {
                var json = args[0];
                var oldKey = args[1];
                var newKey = args[2];

                var jsonMapping = json.TypeMapping ?? JsonbMapping;
                var stringMapping = oldKey.TypeMapping ?? TextMapping;

                var typedJson = EnsureTypeMapping(json, jsonMapping);
                var typedOldKey = EnsureTypeMapping(oldKey, stringMapping);
                var typedNewKey = EnsureTypeMapping(newKey, stringMapping);

                var jsonMinusOldKey = new SqlBinaryExpression(
                    ExpressionType.Subtract,
                    typedJson,
                    typedOldKey,
                    json.Type,
                    jsonMapping);

                var extractedValue = ExtractPath(typedJson, typedOldKey, jsonMapping);
                var newObject = JsonbBuildObject(typedJson, typedNewKey, extractedValue, jsonMapping);

                return JsonbConcat(jsonMinusOldKey, newObject, jsonMapping);
            });
    }

    private static SqlFunctionExpression JsonbConcat(
        SqlExpression target,
        SqlExpression payload,
        RelationalTypeMapping? jsonbMapping)
    {
        var mapping = jsonbMapping ?? target.TypeMapping ?? JsonbMapping;
        var typedTarget = EnsureTypeMapping(target, mapping);
        var typedPayload = EnsureTypeMapping(payload, mapping);

        return new SqlFunctionExpression(
            functionName: "jsonb_concat",
            arguments: [typedTarget, typedPayload],
            nullable: true,
            argumentsPropagateNullability: [false, false],
            type: target.Type,
            typeMapping: mapping);
    }

    private static SqlFunctionExpression JsonbBuildObject(
        SqlExpression target,
        SqlExpression key,
        SqlExpression value,
        RelationalTypeMapping? jsonbMapping)
    {
        var mapping = jsonbMapping ?? target.TypeMapping ?? JsonbMapping;
        var stringMapping = key.TypeMapping ?? TextMapping;

        var typedKey = EnsureTypeMapping(key, stringMapping);
        var typedValue = EnsureTypeMapping(value, mapping);

        return new SqlFunctionExpression(
            functionName: JsonbBuildObjectFunction,
            arguments: [typedKey, typedValue],
            nullable: true,
            argumentsPropagateNullability: [false, false],
            type: target.Type,
            typeMapping: mapping);
    }

    private static SqlFunctionExpression ExtractPath(
        SqlExpression json,
        SqlExpression key,
        RelationalTypeMapping? jsonbMapping)
    {
        var mapping = jsonbMapping ?? json.TypeMapping ?? JsonbMapping;
        var stringMapping = key.TypeMapping ?? TextMapping;

        var typedJson = EnsureTypeMapping(json, mapping);
        var typedKey = EnsureTypeMapping(key, stringMapping);

        return new SqlFunctionExpression(
            functionName: JsonbExtractPathFunction,
            arguments: [typedJson, typedKey],
            nullable: true,
            argumentsPropagateNullability: [false, false],
            type: typeof(JsonNode),
            typeMapping: mapping);
    }

    private static SqlExpression EnsureTypeMapping(
        SqlExpression expression,
        RelationalTypeMapping fallbackMapping)
    {
        return expression.TypeMapping is not null
            ? expression
            : expression switch
            {
                SqlParameterExpression paramExpr => new SqlParameterExpression(paramExpr.Name, paramExpr.Type, fallbackMapping),
                SqlConstantExpression constantExpr => new SqlConstantExpression(constantExpr.Value, constantExpr.Type, fallbackMapping),
                _ => expression,
            };
    }
}
