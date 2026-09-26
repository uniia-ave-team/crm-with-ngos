namespace Crm.Domain.Entities;

/// <summary>
/// Represents a custom key-value pair field associated with a user profile.
/// Inherits from <see cref="BaseEntity"/>.
/// </summary>
public class UserCustomField : BaseEntity
{
    /// <summary>
    /// Gets or sets the foreign key identifier of the associated <see cref="User"/>.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the parent <see cref="User"/>.
    /// </summary>
    public User User { get; set; }

    /// <summary>
    /// Gets or sets the unique key or name of the custom field.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the string value of the custom field.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
