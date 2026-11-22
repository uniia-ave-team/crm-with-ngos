using Crm.Domain.Interfaces.Entities;

namespace Crm.Domain.Entities.Base;

/// <summary>
/// Serves as the abstract base class for domain entities, providing a unique identifier.
/// </summary>
public abstract class BaseEntity : IEntity
{
    /// <summary>
    /// Gets the unique identifier for the entity.
    /// </summary>
    public Guid Id { get; init; }
}
