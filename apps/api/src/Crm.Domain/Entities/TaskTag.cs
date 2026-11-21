namespace Crm.Domain.Entities;

/// <summary>
/// Represents a direct join entity associating a project-scoped <see cref="Entities.Tag"/> with a <see cref="ProjectTask"/>.
/// </summary>
public class TaskTag
{
    /// <summary>
    /// Gets or sets the foreign key identifier of the <see cref="Entities.Tag"/>.
    /// </summary>
    public Guid TagId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the <see cref="Entities.Tag"/>.
    /// </summary>
    public Tag Tag { get; set; }

    /// <summary>
    /// Gets or sets the foreign key identifier of the <see cref="ProjectTask"/>.
    /// </summary>
    public Guid TaskId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the <see cref="ProjectTask"/>.
    /// </summary>
    public ProjectTask Task { get; set; }
}
