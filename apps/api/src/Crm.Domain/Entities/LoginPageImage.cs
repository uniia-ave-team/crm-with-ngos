namespace Crm.Domain.Entities;

/// <summary>
/// Represents the custom image configuration for the application's login page,
/// associated with the primary Non-Governmental Organization (NGO).
/// Inherits from <see cref="BaseEntity"/>.
/// </summary>
public class LoginPageImage : BaseEntity
{
    /// <summary>
    /// Gets or sets the URL or storage path pointing to the login page background or banner image.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique identifier of the associated NGO.
    /// </summary>
    public Guid NgoId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the associated NGO instance.
    /// </summary>
    public Ngo? Ngo { get; set; }
}
