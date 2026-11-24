using Crm.Domain.Attributes;
using Crm.Domain.Entities.Base;
using Crm.Domain.Interfaces.Entities;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents a custom key-value pair field associated with a user profile.
/// Inherits from <see cref="BaseEntity"/>.
/// </summary>
[Auditable]
public class UserCustomField : BaseEntity, IAuditableEntity
{
    /// <summary>
    /// Gets or sets the foreign key identifier of the associated <see cref="User"/>.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the parent <see cref="User"/>.
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Gets or sets the unique key or name of the custom field.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the string value of the custom field.
    /// </summary>
    // TODO: Support FieldDataType (Date, Number, Text, etc.) similar to CustomField in Custom Tables, replacing flat string with typed validation and storage.
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this custom field is visible to other regular users.
    /// If false, it is private and typically only visible to the owner and administrators.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when this custom field was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when this custom field was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
