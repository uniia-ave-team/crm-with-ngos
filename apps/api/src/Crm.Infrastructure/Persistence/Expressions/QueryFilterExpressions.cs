using System.Linq.Expressions;
using Crm.Infrastructure.Persistence.Extensions;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Expressions;

/// <summary>
/// Provides reusable SQL-translatable LINQ expression helpers for parameterized, case-insensitive <c>ILIKE</c> filtering
/// powered by <c>LinqKit</c> expression composition.
/// </summary>
internal static class QueryFilterExpressions
{
    private const string LikeEscapeChar = PostgresSearchStringExtensions.DefaultLikeEscapeChar;

    /// <summary>
    /// Builds a parameterized SQL-translatable <c>ILIKE</c> predicate checking whether <paramref name="propertySelector"/>
    /// equals <paramref name="value"/> (case-insensitive, escaped).
    /// </summary>
    /// <typeparam name="T">The entity type being filtered.</typeparam>
    /// <param name="propertySelector">An expression selecting the string property to match.</param>
    /// <param name="value">The exact value to match case-insensitively.</param>
    /// <returns>An expression translating to parameterized PostgreSQL exact <c>ILIKE</c>.</returns>
    public static Expression<Func<T, bool>> EqualsIgnoreCase<T>(
        Expression<Func<T, string?>> propertySelector,
        string value)
        => BuildILikePredicate(propertySelector, value.ToExactLikePattern(LikeEscapeChar));

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
        => BuildILikePredicate(propertySelector, searchTerm.ToContainsLikePattern(LikeEscapeChar));

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

        string pattern = searchTerm.ToContainsLikePattern(LikeEscapeChar);
        Expression<Func<T, string?>> firstSelector = propertySelectors[0];
        ArgumentNullException.ThrowIfNull(firstSelector);

        // Capturing pattern in a single outer closure makes EF Core reuse ONE @pattern SQL parameter across all OR branches.
        Expression<Func<T, bool>> combined = x =>
            firstSelector.Invoke(x) != null
            && EF.Functions.ILike(firstSelector.Invoke(x)!, pattern, LikeEscapeChar);

        for (int i = 1; i < propertySelectors.Length; i++)
        {
            Expression<Func<T, string?>> selector = propertySelectors[i];
            ArgumentNullException.ThrowIfNull(selector);

            combined = combined.Or(x =>
                selector.Invoke(x) != null
                && EF.Functions.ILike(selector.Invoke(x)!, pattern, LikeEscapeChar));
        }

        return combined.Expand();
    }

    private static Expression<Func<T, bool>> BuildILikePredicate<T>(
        Expression<Func<T, string?>> propertySelector,
        string pattern)
    {
        ArgumentNullException.ThrowIfNull(propertySelector);

        // Capturing pattern in the lambda forces EF Core to bind it as a SQL parameter (@pattern)
        // instead of baking a constant literal into the compiled query cache key.
        Expression<Func<T, bool>> predicate = x =>
            propertySelector.Invoke(x) != null
            && EF.Functions.ILike(propertySelector.Invoke(x)!, pattern, LikeEscapeChar);

        return predicate.Expand();
    }
}
