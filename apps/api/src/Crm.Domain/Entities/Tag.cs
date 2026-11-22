using Crm.Domain.Attributes;
using Crm.Domain.Entities.Base;
using Crm.Domain.Interfaces.Entities;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents a label/tag strictly scoped to a specific <see cref="Project"/> so tags from private projects do not leak globally.
/// </summary>
[Auditable]
public class Tag : BaseEntity, INormalizableTitle, IAuditableEntity
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
    /// Gets or sets the display title of the tag.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the trimmed, uppercase-invariant title used for collation-independent uniqueness checks.
    /// </summary>
    [AuditIgnore]
    public string NormalizedTitle { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when this tag was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when this tag was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets the collection of tasks associated with this tag.
    /// </summary>
    public ICollection<ProjectTask> Tasks { get; init; } = [];
}
