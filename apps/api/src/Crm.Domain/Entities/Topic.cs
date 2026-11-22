using Crm.Domain.Attributes;
using Crm.Domain.Entities.Base;
using Crm.Domain.Interfaces.Entities;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents a board column or section within a project (e.g., "Development", "SMM", "Documentation").
/// </summary>
[Auditable]
public class Topic : BaseEntity, INormalizableTitle, IAuditableEntity, IOrderedEntity
{
    /// <summary>
    /// Gets or sets the foreign key identifier of the parent <see cref="Project"/>.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the parent <see cref="Project"/>.
    /// </summary>
    public Project Project { get; set; } = null!;

    /// <summary>
    /// Gets or sets the title of the topic/section.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the trimmed, uppercase-invariant title used for collation-independent uniqueness checks.
    /// </summary>
    [AuditIgnore]
    public string NormalizedTitle { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display order of the column on the board.
    /// </summary>
    public byte Order { get; set; }

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when this topic was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when this topic was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets the collection of tasks placed under this topic/section.
    /// </summary>
    public ICollection<ProjectTask> Tasks { get; init; } = [];
}
