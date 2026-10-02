using Crm.Domain.Interfaces;

namespace Crm.Domain.Entities;

/// <summary>
/// Serves as the abstract base class for domain entities, providing a unique identifier.
/// </summary>
public abstract class BaseEntity : IEntity
{
    protected BaseEntity()
    {
    }

    protected BaseEntity(Guid id)
    {
        Id = id;
    }

    /// <summary>
    /// Gets the unique identifier for the entity.
    /// </summary>
    public Guid Id { get; init; }
}
