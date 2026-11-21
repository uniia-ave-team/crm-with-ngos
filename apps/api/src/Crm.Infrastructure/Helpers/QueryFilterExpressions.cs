using System.Linq.Expressions;
using System.Reflection;
using Crm.Domain.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Helpers;

/// <summary>
/// Provides reusable SQL-translatable LINQ expression helpers for case-insensitive <c>ILIKE</c> filtering.
/// </summary>
internal static class QueryFilterExpressions
{
    private const string LikeEscapeChar = "\\";

    private static readonly MethodInfo ILikeMethod = typeof(NpgsqlDbFunctionsExtensions).GetMethod(
        nameof(NpgsqlDbFunctionsExtensions.ILike),
        [typeof(DbFunctions), typeof(string), typeof(string), typeof(string)])!;

    /// <summary>
    /// Builds a SQL-translatable <c>ILIKE</c> predicate checking whether <paramref name="propertySelector"/>
    /// contains <paramref name="searchTerm"/> (case-insensitive, escaped).
    /// </summary>
    /// <typeparam name="T">The entity type being filtered.</typeparam>
    /// <param name="propertySelector">An expression selecting the string property to search.</param>
    /// <param name="searchTerm">The non-empty search term.</param>
    /// <returns>An expression translating to PostgreSQL <c>ILIKE</c>.</returns>
    public static Expression<Func<T, bool>> ContainsIgnoreCase<T>(
        Expression<Func<T, string?>> propertySelector,
        string searchTerm)
    {
        ArgumentNullException.ThrowIfNull(propertySelector);

        string pattern = searchTerm.ToContainsLikePattern(LikeEscapeChar);
        var ilikeCall = BuildILikeCall(propertySelector.Body, pattern);

        return Expression.Lambda<Func<T, bool>>(ilikeCall, propertySelector.Parameters);
    }

    /// <summary>
    /// Builds a SQL-translatable <c>ILIKE</c> predicate checking whether either <paramref name="primarySelector"/>
    /// or non-null <paramref name="secondarySelector"/> contains <paramref name="searchTerm"/>.
    /// </summary>
    /// <typeparam name="T">The entity type being filtered.</typeparam>
    /// <param name="primarySelector">An expression selecting the required string property.</param>
    /// <param name="secondarySelector">An expression selecting the optional nullable string property.</param>
    /// <param name="searchTerm">The non-empty search term.</param>
    /// <returns>An expression translating to PostgreSQL <c>ILIKE</c> across both properties.</returns>
    public static Expression<Func<T, bool>> ContainsIgnoreCase<T>(
        Expression<Func<T, string>> primarySelector,
        Expression<Func<T, string?>> secondarySelector,
        string searchTerm)
    {
        ArgumentNullException.ThrowIfNull(primarySelector);
        ArgumentNullException.ThrowIfNull(secondarySelector);

        ParameterExpression parameter = primarySelector.Parameters[0];
        Expression secondaryBody = new ParameterReplacer(secondarySelector.Parameters[0], parameter)
            .Visit(secondarySelector.Body);

        string pattern = searchTerm.ToContainsLikePattern(LikeEscapeChar);

        Expression primaryMatch = BuildILikeCall(primarySelector.Body, pattern);
        Expression secondaryNotNull = Expression.NotEqual(secondaryBody, Expression.Constant(null, typeof(string)));
        Expression secondaryMatch = Expression.AndAlso(secondaryNotNull, BuildILikeCall(secondaryBody, pattern));

        return Expression.Lambda<Func<T, bool>>(Expression.OrElse(primaryMatch, secondaryMatch), parameter);
    }

    private static MethodCallExpression BuildILikeCall(Expression target, string pattern)
        => Expression.Call(
            ILikeMethod,
            Expression.Constant(EF.Functions),
            target,
            Expression.Constant(pattern),
            Expression.Constant(LikeEscapeChar));

    private sealed class ParameterReplacer(ParameterExpression source, Expression replacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == source ? replacement : base.VisitParameter(node);
    }
}
