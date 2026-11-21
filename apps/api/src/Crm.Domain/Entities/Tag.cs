using Crm.Domain.Extensions;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents a label/tag strictly scoped to a specific <see cref="Project"/> so tags from private projects do not leak globally.
/// </summary>
public class Tag : BaseEntity
{
    /// <summary>
    /// Gets or sets the foreign key identifier of the parent <see cref="Project"/>.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the parent <see cref="Project"/>.
    /// </summary>
    public Project Project { get; set; }

    /// <summary>
    /// Gets or sets the display title of the tag, updating <see cref="NormalizedTitle"/> consistently.
    /// </summary>
    public string Title
    {
        get;
        set
        {
            field = value;
            NormalizedTitle = value.NormalizeTitle();
        }
    }

    = string.Empty;

    /// <summary>
    /// Gets the trimmed, uppercase-invariant title used for collation-independent uniqueness checks.
    /// </summary>
    public string NormalizedTitle { get; private set; } = string.Empty;

    /// <summary>
    /// Gets or sets the collection of tasks associated with this tag.
    /// </summary>
    public ICollection<ProjectTask> Tasks { get; set; } = [];
}
