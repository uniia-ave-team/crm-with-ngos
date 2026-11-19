using System.Linq.Expressions;
using Crm.Domain.Entities;
using Crm.Domain.Enums;

namespace Crm.Domain.Expressions;

/// <summary>
/// Provides reusable SQL-translatable LINQ expressions for evaluating <see cref="Project"/> visibility.
/// </summary>
public static class ProjectVisibilityExpressions
{
    /// <summary>
    /// Builds a SQL-translatable predicate matching projects visible to <paramref name="userId"/>.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>An expression evaluating whether a project is visible to the specified user.</returns>
    public static Expression<Func<Project, bool>> IsVisibleTo(Guid userId)
        => p => p.Visibility != ProjectVisibility.Private
                || p.CreatedBy == userId
                || p.Members.Any(m => m.UserId == userId);

    /// <summary>
    /// Composes <see cref="IsVisibleTo"/> with a navigation selector to produce a SQL-translatable visibility predicate
    /// for entities referencing a parent <see cref="Project"/> (e.g., <see cref="ProjectTask"/>).
    /// </summary>
    /// <typeparam name="TSource">The entity type referencing a <see cref="Project"/>.</typeparam>
    /// <param name="projectSelector">An expression selecting the parent <see cref="Project"/> from <typeparamref name="TSource"/>.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>A composed SQL-translatable predicate matching entities whose parent project is visible to <paramref name="userId"/>.</returns>
    public static Expression<Func<TSource, bool>> HasVisibleProject<TSource>(
        Expression<Func<TSource, Project>> projectSelector,
        Guid userId)
    {
        ArgumentNullException.ThrowIfNull(projectSelector);

        Expression<Func<Project, bool>> visibilityPredicate = IsVisibleTo(userId);
        ParameterExpression parameter = projectSelector.Parameters[0];
        Expression body = new ParameterReplacer(visibilityPredicate.Parameters[0], projectSelector.Body)
            .Visit(visibilityPredicate.Body);

        return Expression.Lambda<Func<TSource, bool>>(body, parameter);
    }

    /// <summary>
    /// Combines two predicates over the same entity type with a logical AND into a single SQL-translatable expression.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="left">The left-hand predicate.</param>
    /// <param name="right">The right-hand predicate.</param>
    /// <returns>A combined predicate representing <paramref name="left"/> AND <paramref name="right"/>.</returns>
    public static Expression<Func<T, bool>> CombineAnd<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        ParameterExpression parameter = left.Parameters[0];
        Expression rightBody = new ParameterReplacer(right.Parameters[0], parameter).Visit(right.Body);
        BinaryExpression combined = Expression.AndAlso(left.Body, rightBody);

        return Expression.Lambda<Func<T, bool>>(combined, parameter);
    }
}
