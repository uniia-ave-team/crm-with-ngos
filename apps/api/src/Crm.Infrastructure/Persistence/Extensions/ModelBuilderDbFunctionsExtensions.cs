using System.Data;
using System.Linq.Expressions;
using System.Text.Json.Nodes;
using Crm.Infrastructure.Persistence.Consts;
using Crm.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Crm.Infrastructure.Persistence.Extensions;

/// <summary>
/// Registers custom PostgreSQL <c>jsonb</c> extraction and schema-patching database function translations on <see cref="ModelBuilder"/>.
/// </summary>
public static class ModelBuilderDbFunctionsExtensions
{
    private const string JsonbExtractPathFunction = "jsonb_extract_path";
    private const string JsonbExtractPathTextFunction = "jsonb_extract_path_text";
    private const string JsonbBuildObjectFunction = "jsonb_build_object";
    private const string JsonbConcatFunction = "jsonb_concat";

    private static readonly RelationalTypeMapping JsonbMapping = new StringTypeMapping(PostgresConstants.Jsonb, DbType.String);
    private static readonly RelationalTypeMapping TextMapping = new StringTypeMapping(PostgresConstants.Text, DbType.String);
    private static readonly RelationalTypeMapping BoolMapping = new BoolTypeMapping(PostgresConstants.Boolean, DbType.Boolean);
    private static readonly RelationalTypeMapping TimestamptzMapping = new GenericTypeMapping(PostgresConstants.TimestampWithTimeZone, typeof(DateTime));
    private static readonly RelationalTypeMapping TimestampWithoutTimeZoneMapping = new GenericTypeMapping(PostgresConstants.TimestampWithoutTimeZone, typeof(DateTime));
    private static readonly SqlFragmentExpression JsonNullSqlFragment = new($"'null'::{PostgresConstants.Jsonb}");

    /// <summary>
    /// Registers custom database function translations for EF Core.
    /// </summary>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    /// <returns>The configured <see cref="ModelBuilder"/> for chaining.</returns>
    public static ModelBuilder AddCustomDbFunctions(this ModelBuilder modelBuilder)
    {
        RegisterJsonbApplyPatch(modelBuilder);
        RegisterJsonbExtractPathText(modelBuilder);

        RegisterTextSafeCast(modelBuilder, nameof(PostgresCastFunctions.SafeCastNumber), PostgresConstants.Numeric, typeof(decimal?));
        RegisterTextSafeCast(modelBuilder, nameof(PostgresCastFunctions.SafeCastBoolean), PostgresConstants.Boolean, typeof(bool?));
        RegisterTextSafeCast(modelBuilder, nameof(PostgresCastFunctions.SafeCastDate), PostgresConstants.Date, typeof(DateOnly?));
        RegisterTextSafeCast(modelBuilder, nameof(PostgresCastFunctions.SafeCastDateTime), PostgresConstants.TimestampWithTimeZone, typeof(DateTime?));

        return modelBuilder;
    }

    private static void RegisterTextSafeCast(
        ModelBuilder modelBuilder,
        string methodName,
        string targetStoreType,
        Type returnType)
    {
        var method = typeof(PostgresCastFunctions).GetMethod(methodName, [typeof(string)])
            ?? throw new InvalidOperationException($"{methodName} registration failed.");

        modelBuilder.HasDbFunction(method)
            .HasStoreType(targetStoreType)
            .HasParameter("value", p => p.HasStoreType(PostgresConstants.Text))
            .HasTranslation(args =>
            {
                var typedText = EnsureTypeMapping(args[0], TextMapping);
                return BuildSafeCastExpression(typedText, targetStoreType, returnType);
            });
    }

    private static void RegisterJsonbExtractPathText(ModelBuilder modelBuilder)
    {
        var method = typeof(JsonbFunctionsExtensions).GetMethod(
            nameof(JsonbFunctionsExtensions.JsonbExtractPathText),
            [typeof(JsonObject), typeof(string)])
            ?? throw new InvalidOperationException($"{nameof(JsonbFunctionsExtensions.JsonbExtractPathText)} registration failed.");

        modelBuilder.HasDbFunction(method)
            .HasStoreType(PostgresConstants.Text)
            .HasParameter("json", p => p.HasStoreType(PostgresConstants.Jsonb))
            .HasParameter("key", p => p.HasStoreType(PostgresConstants.Text))
            .HasTranslation(args =>
            {
                var typedJson = EnsureTypeMapping(args[0], JsonbMapping);
                var typedKey = EnsureTypeMapping(args[1], TextMapping);

                return new SqlFunctionExpression(
                    functionName: JsonbExtractPathTextFunction,
                    arguments: [typedJson, typedKey],
                    nullable: true,
                    argumentsPropagateNullability: [true, true],
                    type: typeof(string),
                    typeMapping: TextMapping);
            });
    }

    private static CaseExpression BuildSafeCastExpression(
        SqlExpression textExpr,
        string targetStoreType,
        Type returnType)
    {
        Type nonNullableType = Nullable.GetUnderlyingType(returnType) ?? returnType;
        var targetMapping = new GenericTypeMapping(targetStoreType, returnType);

        bool isDate = string.Equals(targetStoreType, PostgresConstants.Date, StringComparison.OrdinalIgnoreCase);
        bool isNumeric = string.Equals(targetStoreType, PostgresConstants.Numeric, StringComparison.OrdinalIgnoreCase);
        bool isTemporal = string.Equals(targetStoreType, PostgresConstants.TimestampWithTimeZone, StringComparison.OrdinalIgnoreCase) || isDate;

        string validCheckStoreType = isDate ? PostgresConstants.TimestampWithTimeZone : targetStoreType;

        SqlExpression isValidFunc = new SqlFunctionExpression(
            functionName: "pg_input_is_valid",
            arguments: [textExpr, new SqlConstantExpression(validCheckStoreType, TextMapping)],
            nullable: true,
            argumentsPropagateNullability: [true, true],
            type: typeof(bool),
            typeMapping: BoolMapping);

        if (isNumeric || isTemporal)
        {
            string exclusionPattern = isNumeric
                ? @"^\s*(NaN|[-+]?Infinity|[-+]?inf)\s*$"
                : @"^\s*[-+]?Infinity\s*$";

            var isSpecialValue = new SqlFunctionExpression(
                functionName: "regexp_like",
                arguments:
                [
                    textExpr,
                    new SqlConstantExpression(exclusionPattern, TextMapping),
                    new SqlConstantExpression("i", TextMapping),
                ],
                nullable: true,
                argumentsPropagateNullability: [true, true, true],
                type: typeof(bool),
                typeMapping: BoolMapping);

            var isNotSpecialValue = new SqlUnaryExpression(
                ExpressionType.Not,
                isSpecialValue,
                typeof(bool),
                BoolMapping);

            isValidFunc = new SqlBinaryExpression(
                ExpressionType.AndAlso,
                isValidFunc,
                isNotSpecialValue,
                typeof(bool),
                BoolMapping);
        }

        SqlExpression castExpr;
        if (isDate)
        {
            var timestamptzCast = new SqlUnaryExpression(
                ExpressionType.Convert,
                textExpr,
                typeof(DateTime),
                TimestamptzMapping);

            // Normalize to UTC before extracting the calendar date so ordering is independent of DB session TimeZone.
            var utcTimestamp = new SqlFunctionExpression(
                functionName: "timezone",
                arguments: [new SqlConstantExpression("UTC", TextMapping), timestamptzCast],
                nullable: true,
                argumentsPropagateNullability: [false, true],
                type: typeof(DateTime),
                typeMapping: TimestampWithoutTimeZoneMapping);

            castExpr = new SqlUnaryExpression(
                ExpressionType.Convert,
                utcTimestamp,
                nonNullableType,
                targetMapping);
        }
        else
        {
            castExpr = new SqlUnaryExpression(
                ExpressionType.Convert,
                textExpr,
                nonNullableType,
                targetMapping);
        }

        return new CaseExpression(
            operand: null,
            whenClauses: [new CaseWhenClause(isValidFunc, castExpr)],
            elseResult: new SqlConstantExpression(null, returnType, targetMapping));
    }

    private static void RegisterJsonbApplyPatch(ModelBuilder modelBuilder)
    {
        var method = typeof(JsonbFunctionsExtensions).GetMethod(
            nameof(JsonbFunctionsExtensions.JsonbApplyPatch),
            [typeof(JsonObject), typeof(string[]), typeof(string[]), typeof(string[]), typeof(string[])])
            ?? throw new InvalidOperationException($"{nameof(JsonbFunctionsExtensions.JsonbApplyPatch)} registration failed.");

        var parameters = method.GetParameters().ToDictionary(p => p.Name!, StringComparer.OrdinalIgnoreCase);

        modelBuilder.HasDbFunction(method)
            .HasStoreType(PostgresConstants.Jsonb)
            .HasParameter(parameters["json"].Name!, p => p.HasStoreType(PostgresConstants.Jsonb))
            .HasParameter(parameters[nameof(SchemaPatch.DropKeys)].Name!, p => p.HasStoreType(PostgresConstants.TextArray))
            .HasParameter(parameters[nameof(SchemaPatch.TargetKeys)].Name!, p => p.HasStoreType(PostgresConstants.TextArray))
            .HasParameter(parameters[nameof(SchemaPatch.SourceKeys)].Name!, p => p.HasStoreType(PostgresConstants.TextArray))
            .HasParameter(parameters[nameof(SchemaPatch.FallbackJson)].Name!, p => p.HasStoreType(PostgresConstants.TextArray))
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
                    var arraySql = string.Join(", ", dropKeys.Select(FormatSqlStringLiteral));
                    var arrayExpr = new SqlFragmentExpression($"ARRAY[{arraySql}]::{PostgresConstants.TextArray}");

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
            return new SqlFragmentExpression($"{FormatSqlStringLiteral(fallbackJson)}::{PostgresConstants.Jsonb}");
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

            var fallbackExpr = new SqlFragmentExpression($"{FormatSqlStringLiteral(fallbackJson)}::{PostgresConstants.Jsonb}");

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

    private static string FormatSqlStringLiteral(string value)
        => value.Contains('\0', StringComparison.Ordinal)
            ? throw new ArgumentException("SQL literal cannot contain null characters.", nameof(value))
            : $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

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

    private sealed class GenericTypeMapping(string storeType, Type clrType) : RelationalTypeMapping(new RelationalTypeMappingParameters(
        new CoreTypeMappingParameters(clrType),
        storeType))
    {
        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
            => new GenericTypeMapping(parameters.StoreType, parameters.CoreParameters.ClrType);
    }
}
