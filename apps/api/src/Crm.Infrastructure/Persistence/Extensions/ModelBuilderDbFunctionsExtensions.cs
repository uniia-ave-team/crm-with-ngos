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
    private const string JsonbExtractPathTextFunction = "jsonb_extract_path_text";
    private const string JsonbBuildObjectFunction = "jsonb_build_object";
    private const string JsonbConcatFunction = "jsonb_concat";

    private static readonly RelationalTypeMapping JsonbMapping = new StringTypeMapping(JsonbStoreType, DbType.String);
    private static readonly RelationalTypeMapping TextMapping = new StringTypeMapping(TextStoreType, DbType.String);
    private static readonly RelationalTypeMapping BoolMapping = new BoolTypeMapping("boolean", DbType.Boolean);
    private static readonly RelationalTypeMapping IntMapping = new IntTypeMapping("integer", DbType.Int32);
    private static readonly SqlFragmentExpression JsonNullSqlFragment = new($"'null'::{JsonbStoreType}");

    /// <summary>
    /// Registers custom database function translations for EF Core.
    /// </summary>
    public static ModelBuilder AddCustomDbFunctions(this ModelBuilder modelBuilder)
    {
        RegisterJsonbApplyPatch(modelBuilder);

        RegisterJsonbExtractPath(modelBuilder, nameof(JsonbFunctionsExtensions.JsonbExtractPathText), TextStoreType, typeof(string));
        RegisterJsonbExtractPath(modelBuilder, nameof(JsonbFunctionsExtensions.JsonbExtractPathNumber), "numeric", typeof(decimal?));
        RegisterJsonbExtractPath(modelBuilder, nameof(JsonbFunctionsExtensions.JsonbExtractPathBoolean), "boolean", typeof(bool?));
        RegisterJsonbExtractPath(modelBuilder, nameof(JsonbFunctionsExtensions.JsonbExtractPathDate), "date", typeof(DateOnly?));
        RegisterJsonbExtractPath(modelBuilder, nameof(JsonbFunctionsExtensions.JsonbExtractPathDateTime), "timestamp with time zone", typeof(DateTime?));

        return modelBuilder;
    }

    private static void RegisterJsonbExtractPath(
        ModelBuilder modelBuilder,
        string methodName,
        string targetStoreType,
        Type returnType)
    {
        var method = typeof(JsonbFunctionsExtensions).GetMethod(
            methodName,
            [typeof(JsonObject), typeof(string)])
            ?? throw new InvalidOperationException($"{methodName} registration failed.");

        Type nonNullableType = Nullable.GetUnderlyingType(returnType) ?? returnType;
        bool isText = returnType == typeof(string);
        var targetMapping = isText ? TextMapping : new GenericTypeMapping(targetStoreType, returnType);

        modelBuilder.HasDbFunction(method)
            .HasStoreType(targetStoreType)
            .HasParameter("json", p => p.HasStoreType(JsonbStoreType))
            .HasParameter("key", p => p.HasStoreType(TextStoreType))
            .HasTranslation(args =>
            {
                var typedJson = EnsureTypeMapping(args[0], JsonbMapping);
                var typedKey = EnsureTypeMapping(args[1], TextMapping);

                var textExtractFunc = new SqlFunctionExpression(
                    functionName: JsonbExtractPathTextFunction,
                    arguments: [typedJson, typedKey],
                    nullable: true,
                    argumentsPropagateNullability: [true, true],
                    type: typeof(string),
                    typeMapping: TextMapping);

                if (isText)
                {
                    return textExtractFunc;
                }

                bool isDate = string.Equals(targetStoreType, "date", StringComparison.OrdinalIgnoreCase);
                bool isNumeric = string.Equals(targetStoreType, "numeric", StringComparison.OrdinalIgnoreCase);
                bool isTemporal = string.Equals(targetStoreType, "timestamp with time zone", StringComparison.OrdinalIgnoreCase) || isDate;

                string validCheckStoreType = isDate ? "timestamp with time zone" : targetStoreType;

                SqlExpression isValidFunc = new SqlFunctionExpression(
                    functionName: "pg_input_is_valid",
                    arguments: [textExtractFunc, new SqlConstantExpression(validCheckStoreType, TextMapping)],
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
                        arguments: [
                            textExtractFunc,
                            new SqlConstantExpression(exclusionPattern, TextMapping),
                            new SqlConstantExpression("i", TextMapping)
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
                    var timestamptzMapping = new GenericTypeMapping("timestamp with time zone", typeof(DateTime));
                    var timestamptzCast = new SqlUnaryExpression(
                        ExpressionType.Convert,
                        textExtractFunc,
                        typeof(DateTime),
                        timestamptzMapping);

                    castExpr = new SqlUnaryExpression(
                        ExpressionType.Convert,
                        timestamptzCast,
                        nonNullableType,
                        targetMapping);
                }
                else
                {
                    castExpr = new SqlUnaryExpression(
                        ExpressionType.Convert,
                        textExtractFunc,
                        nonNullableType,
                        targetMapping);
                }

                return new CaseExpression(
                    operand: null,
                    whenClauses: [new CaseWhenClause(isValidFunc, castExpr)],
                    elseResult: new SqlConstantExpression(null, returnType, targetMapping));
            });
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

    private sealed class GenericTypeMapping(string storeType, Type clrType) : RelationalTypeMapping(new RelationalTypeMappingParameters(
        new CoreTypeMappingParameters(clrType),
        storeType))
    {
        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
            => new GenericTypeMapping(parameters.StoreType, parameters.CoreParameters.ClrType);
    }
}
