namespace Crm.Domain.Interfaces.Entities;

/// <summary>
/// Represents a domain entity that tracks its UTC+0 creation timestamp.
/// </summary>
public interface ICreatableEntity
{
    /// <summary>
    /// Gets or sets the UTC+0 timestamp indicating when the entity was created.
    /// </summary>
    DateTime CreatedAt { get; set; }
}
