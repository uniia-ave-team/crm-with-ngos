using Crm.Domain.Attributes;
using Crm.Domain.Entities.Base;
using Crm.Domain.Interfaces.Entities;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents the Non-Governmental Organization (NGO) instance within the system,
/// enforcing the single-instance constraint where the application serves a single primary organization.
/// Inherits from <see cref="BaseEntity"/> and manages organizational details and associated system users.
/// </summary>
[Auditable]
public class Ngo : BaseEntity, IAuditableEntity
{
    /// <summary>
    /// Gets or sets the official name of the NGO.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional URL or storage path pointing to the NGO's logo image.
    /// </summary>
    public string? Logo { get; set; }

    /// <summary>
    /// Gets or sets the UTC+0 timestamp indicating when the NGO instance was initially created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC+0 timestamp indicating when the NGO instance was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets the collection of business user profiles belonging to this NGO instance.
    /// </summary>
    public ICollection<User> Users { get; init; } = [];

    /// <summary>
    /// Gets the collection of actual custom tables (instances) created by this NGO.
    /// </summary>
    public ICollection<CustomTable> CustomTables { get; init; } = [];
}
