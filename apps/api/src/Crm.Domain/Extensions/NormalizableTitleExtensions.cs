using Crm.Domain.Interfaces.Entities;

namespace Crm.Domain.Extensions;

/// <summary>
/// Provides extension methods for setting and synchronizing display and normalized titles
/// on entities implementing <see cref="INormalizableTitle"/>.
/// </summary>
public static class NormalizableTitleExtensions
{
    /// <summary>
    /// Trims <paramref name="title"/> and assigns both <see cref="INormalizableTitle.Title"/>
    /// and <see cref="INormalizableTitle.NormalizedTitle"/> consistently.
    /// </summary>
    /// <typeparam name="T">The entity type implementing <see cref="INormalizableTitle"/>.</typeparam>
    /// <param name="entity">The target entity instance.</param>
    /// <param name="title">The raw title value to assign.</param>
    /// <returns>The updated entity instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> or <paramref name="title"/> is <c>null</c>.</exception>
    public static T SetTitle<T>(this T entity, string title)
        where T : INormalizableTitle
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(title);

        string trimmed = title.Trim();
        entity.Title = trimmed;
        entity.NormalizedTitle = trimmed.NormalizeTitle();

        return entity;
    }

    /// <summary>
    /// Synchronizes <see cref="INormalizableTitle.Title"/> (trimmed) and
    /// <see cref="INormalizableTitle.NormalizedTitle"/> from the entity's current title value.
    /// </summary>
    /// <typeparam name="T">The entity type implementing <see cref="INormalizableTitle"/>.</typeparam>
    /// <param name="entity">The target entity instance.</param>
    /// <returns>The updated entity instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is <c>null</c>.</exception>
    public static T SyncNormalizedTitle<T>(this T entity)
        where T : INormalizableTitle
    {
        ArgumentNullException.ThrowIfNull(entity);
        return entity.SetTitle(entity.Title);
    }
}
