namespace Crm.Domain.Interfaces.Entities;

/// <summary>
/// Represents a domain entity that stores both a display title and its normalized uppercase-invariant counterpart
/// for collation-independent uniqueness checks.
/// </summary>
public interface INormalizableTitle
{
    /// <summary>
    /// Gets or sets the display title of the entity.
    /// </summary>
    string Title { get; set; }

    /// <summary>
    /// Gets or sets the trimmed, uppercase-invariant title used for collation-independent uniqueness checks.
    /// </summary>
    string NormalizedTitle { get; set; }
}
