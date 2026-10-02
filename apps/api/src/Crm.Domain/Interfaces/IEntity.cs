namespace Crm.Domain.Interfaces;

/// <summary>
/// Represents the base contract for all domain entities,
/// guaranteeing the presence of a unique identifier of type <see cref="Guid"/>.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// Gets the unique identifier for the entity.
    /// </summary>
    /// <value>A <see cref="Guid"/> value that uniquely identifies the entity instance.</value>
    Guid Id { get; }
}
