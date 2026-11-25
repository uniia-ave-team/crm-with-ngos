using System.Linq.Expressions;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using LinqKit;

namespace Crm.Infrastructure.Persistence.Expressions;

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
                || p.Members.Any(m => m.UserId == userId);

    /// <summary>
    /// Composes <see cref="IsVisibleTo"/> with a navigation selector via <c>LinqKit</c> to produce a
    /// SQL-translatable visibility predicate for entities referencing a parent <see cref="Project"/>.
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

        Expression<Func<Project, bool>> visibility = IsVisibleTo(userId);
        Expression<Func<TSource, bool>> composed = entity => visibility.Invoke(projectSelector.Invoke(entity));

        return composed.Expand();
    }
}
