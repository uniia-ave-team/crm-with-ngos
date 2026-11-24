namespace Crm.Domain.Interfaces.Entities;

/// <summary>
/// Represents a domain entity that supports soft deletion while retaining its historical record in storage.
/// </summary>
public interface ISoftDeletableEntity
{
    /// <summary>
    /// Gets or sets a value indicating whether the entity has been soft-deleted.
    /// </summary>
    bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the optional UTC+0 timestamp indicating when the entity was soft-deleted.
    /// </summary>
    DateTime? DeletedAt { get; set; }
}
