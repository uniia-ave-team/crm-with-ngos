using Crm.Domain.Enums;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents the extended business profile of a system member within the NGO.
/// Inherits from <see cref="BaseEntity"/> and maintains personal details, contact information and operational status.
/// </summary>
public class User : BaseEntity
{
    /// <summary>
    /// Gets or sets the first name of the user.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last name (surname) of the user.
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional patronymic (middle name) of the user.
    /// </summary>
    public string? Patronymic { get; set; }

    /// <summary>
    /// Gets or sets the internal organizational role, title, or position held by the user.
    /// </summary>
    public string? InternalPosition { get; set; }

    /// <summary>
    /// Gets or sets the contact phone number of the user.
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the country of residence or origin for the user.
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Gets or sets emergency contact details (name and phone number) for safety purposes.
    /// </summary>
    public string? EmergencyContact { get; set; }

    /// <summary>
    /// Gets or sets the user's preferred interface or communication language code (e.g., "en", "uk").
    /// </summary>
    public string? PreferredLanguage { get; set; }

    /// <summary>
    /// Gets or sets the user's preferred pronouns (e.g., "he/him", "she/her", "they/them").
    /// </summary>
    public string? Pronouns { get; set; }

    /// <summary>
    /// Gets or sets the grammatical category of the user's pronouns for system logic and role declension.
    /// </summary>
    public PronounCategory? PronounCategory { get; set; }

    /// <summary>
    /// Gets or sets the URL or path to the user's avatar image.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user account is active.
    /// If false, the account is soft-deleted or deactivated. Default is true.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC timestamp when the user profile was initially created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the unique foreign key identifier of the associated <see cref="Ngo"/> instance.
    /// </summary>
    public Guid? NgoId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the parent <see cref="Ngo"/> organization.
    /// </summary>
    public Ngo? Ngo { get; set; }

    /// <summary>
    /// Gets or sets the collection of custom key-value fields associated with this user.
    /// </summary>
    public ICollection<UserCustomField> CustomFields { get; set; } = [];
}
