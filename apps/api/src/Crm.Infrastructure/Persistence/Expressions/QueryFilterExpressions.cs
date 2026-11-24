using System.Linq.Expressions;
using System.Reflection;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Expressions;

/// <summary>
/// Provides reusable SQL-translatable LINQ expression helpers for parameterized, case-insensitive <c>ILIKE</c> filtering.
/// </summary>
internal static class QueryFilterExpressions
{
    private const string LikeEscapeChar = "\\";

    private static readonly MethodInfo ILikeMethod = typeof(NpgsqlDbFunctionsExtensions).GetMethod(
        nameof(NpgsqlDbFunctionsExtensions.ILike),
        [typeof(DbFunctions), typeof(string), typeof(string), typeof(string)])!;

    /// <summary>
    /// Builds a parameterized SQL-translatable <c>ILIKE</c> predicate checking whether <paramref name="propertySelector"/>
    /// contains <paramref name="searchTerm"/> (case-insensitive, escaped).
    /// </summary>
    /// <typeparam name="T">The entity type being filtered.</typeparam>
    /// <param name="propertySelector">An expression selecting the string property to search.</param>
    /// <param name="searchTerm">The non-empty search term.</param>
    /// <returns>An expression translating to parameterized PostgreSQL <c>ILIKE</c>.</returns>
    public static Expression<Func<T, bool>> ContainsIgnoreCase<T>(
        Expression<Func<T, string?>> propertySelector,
        string searchTerm)
    {
        ArgumentNullException.ThrowIfNull(propertySelector);

        Expression patternExpression = CreateParameterizedPattern(searchTerm);
        Expression match = BuildNullSafeILikeCall(propertySelector.Body, patternExpression);

        return Expression.Lambda<Func<T, bool>>(match, propertySelector.Parameters);
    }

    /// <summary>
    /// Builds a parameterized SQL-translatable <c>ILIKE</c> predicate checking whether any of the provided
    /// <paramref name="propertySelectors"/> contains <paramref name="searchTerm"/> (case-insensitive, escaped).
    /// </summary>
    /// <typeparam name="T">The entity type being filtered.</typeparam>
    /// <param name="searchTerm">The non-empty search term.</param>
    /// <param name="propertySelectors">One or more expressions selecting string properties to search.</param>
    /// <returns>An expression translating to parameterized PostgreSQL <c>ILIKE</c> joined with <c>OR</c>.</returns>
    public static Expression<Func<T, bool>> ContainsIgnoreCase<T>(
        string searchTerm,
        params Expression<Func<T, string?>>[] propertySelectors)
    {
        ArgumentNullException.ThrowIfNull(propertySelectors);

        if (propertySelectors.Length == 0)
        {
            throw new ArgumentException("At least one property selector must be provided.", nameof(propertySelectors));
        }

        Expression patternExpression = CreateParameterizedPattern(searchTerm);
        ParameterExpression parameter = propertySelectors[0].Parameters[0];

        Expression combined = BuildNullSafeILikeCall(propertySelectors[0].Body, patternExpression);

        for (int i = 1; i < propertySelectors.Length; i++)
        {
            Expression reboundBody = new ParameterReplacer(propertySelectors[i].Parameters[0], parameter)
                .Visit(propertySelectors[i].Body);

            Expression match = BuildNullSafeILikeCall(reboundBody, patternExpression);
            combined = Expression.OrElse(combined, match);
        }

        return Expression.Lambda<Func<T, bool>>(combined, parameter);
    }

    private static Expression CreateParameterizedPattern(string searchTerm)
    {
        string pattern = searchTerm.ToContainsLikePattern(LikeEscapeChar);

        // Wrapping the pattern in a closure forces EF Core to bind it as a SQL parameter (@pattern)
        // instead of baking a constant literal into the compiled query cache key.
        Expression<Func<string>> patternClosure = () => pattern;
        return patternClosure.Body;
    }

    private static BinaryExpression BuildNullSafeILikeCall(Expression target, Expression patternExpression)
    {
        Expression notNull = Expression.NotEqual(target, Expression.Constant(null, typeof(string)));
        MethodCallExpression ilikeCall = Expression.Call(
            ILikeMethod,
            Expression.Constant(EF.Functions),
            target,
            patternExpression,
            Expression.Constant(LikeEscapeChar));

        return Expression.AndAlso(notNull, ilikeCall);
    }
}
