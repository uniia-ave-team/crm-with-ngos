using Crm.Domain.Extensions;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents a board column or section within a project (e.g., "Development", "SMM", "Documentation").
/// </summary>
public class Topic : BaseEntity
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
    /// Gets or sets the title of the topic/section, updating <see cref="NormalizedTitle"/> consistently.
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
    /// Gets or sets the display order of the column on the board.
    /// </summary>
    public byte Order { get; set; }

    /// <summary>
    /// Gets or sets the collection of tasks placed under this topic/section.
    /// </summary>
    public ICollection<ProjectTask> Tasks { get; set; } = [];
}
