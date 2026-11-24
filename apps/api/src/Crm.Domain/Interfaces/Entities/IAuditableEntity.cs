namespace Crm.Domain.Interfaces.Entities;

/// <summary>
/// Represents a domain entity that tracks both its UTC+0 creation and last-modification timestamps.
/// </summary>
public interface IAuditableEntity : ICreatableEntity
{
    /// <summary>
    /// Gets or sets the UTC+0 timestamp indicating when the entity was last updated.
    /// </summary>
    DateTime UpdatedAt { get; set; }
}
