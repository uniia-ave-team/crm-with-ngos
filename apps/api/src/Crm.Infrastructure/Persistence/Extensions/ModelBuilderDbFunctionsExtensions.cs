using System.Data;
using System.Linq.Expressions;
using System.Text.Json.Nodes;
using Crm.Domain.Common;
using Crm.Infrastructure.Persistence.Extensions.FunctionExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Crm.Infrastructure.Persistence.Extensions;

public static class ModelBuilderDbFunctionsExtensions
{
    private const string JsonbStoreType = "jsonb";
    private const string TextStoreType = "text";
    private const string TextArrayStoreType = "text[]";
    private const string JsonbExtractPathFunction = "jsonb_extract_path";
    private const string JsonbBuildObjectFunction = "jsonb_build_object";
    private const string JsonbConcatFunction = "jsonb_concat";

    private static readonly RelationalTypeMapping JsonbMapping = new StringTypeMapping(JsonbStoreType, DbType.String);
    private static readonly RelationalTypeMapping TextMapping = new StringTypeMapping(TextStoreType, DbType.String);
    private static readonly SqlFragmentExpression JsonNullSqlFragment = new($"'null'::{JsonbStoreType}");

    /// <summary>
    /// Registers custom database function translations for EF Core.
    /// </summary>
    public static ModelBuilder AddCustomDbFunctions(this ModelBuilder modelBuilder)
    {
        RegisterJsonbApplyPatch(modelBuilder);

        return modelBuilder;
    }

    private static void RegisterJsonbApplyPatch(ModelBuilder modelBuilder)
    {
        var method = typeof(JsonbFunctionsExtensions).GetMethod(
            nameof(JsonbFunctionsExtensions.JsonbApplyPatch),
            [typeof(JsonObject), typeof(string[]), typeof(string[]), typeof(string[]), typeof(string[])])
            ?? throw new InvalidOperationException("JsonbApplyPatch registration failed.");

        var parameters = method.GetParameters().ToDictionary(p => p.Name!, StringComparer.OrdinalIgnoreCase);

        modelBuilder.HasDbFunction(method)
            .HasStoreType(JsonbStoreType)
            .HasParameter(parameters["json"].Name!, p => p.HasStoreType(JsonbStoreType))
            .HasParameter(parameters[nameof(SchemaPatch.DropKeys)].Name!, p => p.HasStoreType(TextArrayStoreType))
            .HasParameter(parameters[nameof(SchemaPatch.TargetKeys)].Name!, p => p.HasStoreType(TextArrayStoreType))
            .HasParameter(parameters[nameof(SchemaPatch.SourceKeys)].Name!, p => p.HasStoreType(TextArrayStoreType))
            .HasParameter(parameters[nameof(SchemaPatch.FallbackJson)].Name!, p => p.HasStoreType(TextArrayStoreType))
            .HasTranslation(args =>
            {
                const int maxPairsPerCall = 50;

                var rawJson = args[0];
                var jsonMapping = rawJson.TypeMapping ?? JsonbMapping;
                var typedJson = EnsureTypeMapping(rawJson, jsonMapping);

                string[] dropKeys = ReadConstArray(args[1]);
                string[] targetKeys = ReadConstArray(args[2]);
                string[] sourceKeys = ReadConstArray(args[3]);
                string[] fallbackJson = ReadConstArray(args[4]);

                SqlExpression baseJson = typedJson;
                if (dropKeys.Length > 0)
                {
                    var arraySql = string.Join(", ", dropKeys.Select(k => $"'{k.Replace("'", "''")}'"));
                    var arrayExpr = new SqlFragmentExpression($"ARRAY[{arraySql}]::{TextArrayStoreType}");

                    baseJson = new SqlBinaryExpression(
                        ExpressionType.Subtract,
                        typedJson,
                        arrayExpr,
                        rawJson.Type,
                        jsonMapping);
                }

                if (targetKeys.Length == 0)
                {
                    return baseJson;
                }

                SqlExpression currentResult = baseJson;

                for (int offset = 0; offset < targetKeys.Length; offset += maxPairsPerCall)
                {
                    int count = Math.Min(maxPairsPerCall, targetKeys.Length - offset);
                    var objArgs = new List<SqlExpression>(count * 2);

                    for (int i = offset; i < offset + count; i++)
                    {
                        string targetKey = targetKeys[i];
                        string sourceKey = sourceKeys[i];
                        string fallback = fallbackJson[i];

                        var targetKeyExpr = new SqlConstantExpression(targetKey, typeof(string), TextMapping);
                        var valueExpr = BuildValueExpression(typedJson, sourceKey, fallback, jsonMapping);

                        objArgs.Add(targetKeyExpr);
                        objArgs.Add(valueExpr);
                    }

                    var buildObjectFunc = new SqlFunctionExpression(
                        functionName: JsonbBuildObjectFunction,
                        arguments: objArgs,
                        nullable: true,
                        argumentsPropagateNullability: objArgs.Select(_ => false),
                        type: rawJson.Type,
                        typeMapping: jsonMapping);

                    currentResult = JsonbConcat(currentResult, buildObjectFunc, jsonMapping);
                }

                return currentResult;
            });
    }

    private static SqlExpression BuildValueExpression(
        SqlExpression jsonLeaf,
        string sourceKey,
        string fallbackJson,
        RelationalTypeMapping jsonMapping)
    {
        bool hasSource = !string.IsNullOrEmpty(sourceKey);
        bool hasFallback = !string.IsNullOrEmpty(fallbackJson);

        if (!hasSource && hasFallback)
        {
            return new SqlFragmentExpression($"'{fallbackJson.Replace("'", "''")}'::jsonb");
        }

        if (hasSource && !hasFallback)
        {
            var sourceKeyExpr = new SqlConstantExpression(sourceKey, typeof(string), TextMapping);
            return ExtractPath(jsonLeaf, sourceKeyExpr, jsonMapping);
        }

        if (hasSource && hasFallback)
        {
            var sourceKeyExpr = new SqlConstantExpression(sourceKey, typeof(string), TextMapping);
            var extractedValue = ExtractPath(jsonLeaf, sourceKeyExpr, jsonMapping);

            var nullIfFunc = new SqlFunctionExpression(
                functionName: "NULLIF",
                arguments: [extractedValue, JsonNullSqlFragment],
                nullable: true,
                argumentsPropagateNullability: [false, false],
                type: typeof(JsonNode),
                typeMapping: jsonMapping);

            var fallbackExpr = new SqlFragmentExpression($"'{fallbackJson.Replace("'", "''")}'::jsonb");

            return new SqlFunctionExpression(
                functionName: "COALESCE",
                arguments: [nullIfFunc, fallbackExpr],
                nullable: true,
                argumentsPropagateNullability: [false, false],
                type: typeof(JsonNode),
                typeMapping: jsonMapping);
        }

        return JsonNullSqlFragment;
    }

    private static string[] ReadConstArray(SqlExpression expression)
    {
        if (expression is SqlConstantExpression sqlConst)
        {
            if (sqlConst.Value is ConstantExpression { Value: string[] arrFromConst })
            {
                return arrFromConst;
            }

            if (sqlConst.Value is string[] arrDirect)
            {
                return arrDirect;
            }
        }

        throw new InvalidOperationException($"Patch arrays must be translated as constant string arrays. Received: {expression.GetType().Name}");
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
            functionName: JsonbConcatFunction,
            arguments: [typedTarget, typedPayload],
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
        var typedJson = EnsureTypeMapping(json, mapping);
        var typedKey = EnsureTypeMapping(key, TextMapping);

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
