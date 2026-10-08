namespace Crm.Domain.Entities;

/// <summary>
/// Represents the Non-Governmental Organization (NGO) instance within the system,
/// enforcing the single-instance constraint where the application serves a single primary organization.
/// Inherits from <see cref="BaseEntity"/> and manages organizational details and associated system users.
/// </summary>
public class Ngo : BaseEntity
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
    /// Gets or sets the UTC timestamp indicating when the NGO instance was initially created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the collection of business user profiles belonging to this NGO instance.
    /// </summary>
    public ICollection<User> Users { get; set; } = [];
}
