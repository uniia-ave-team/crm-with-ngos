namespace Crm.Domain.Entities;

/// <summary>
/// Represents a direct join entity linking an assigned <see cref="Entities.User"/> to a <see cref="ProjectTask"/>.
/// </summary>
public class TaskUser
{
    /// <summary>
    /// Gets or sets the foreign key identifier of the <see cref="ProjectTask"/>.
    /// </summary>
    public Guid TaskId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the <see cref="ProjectTask"/>.
    /// </summary>
    public ProjectTask Task { get; set; }

    /// <summary>
    /// Gets or sets the foreign key identifier of the assigned <see cref="Entities.User"/>.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the assigned <see cref="Entities.User"/>.
    /// </summary>
    public User User { get; set; }
}
