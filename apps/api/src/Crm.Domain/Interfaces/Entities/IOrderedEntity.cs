namespace Crm.Domain.Interfaces.Entities;

/// <summary>
/// Represents an entity or schema definition that participates in a contiguous 0-based display order sequence.
/// </summary>
public interface IOrderedEntity
{
    /// <summary>
    /// Gets the 0-based display order index.
    /// </summary>
    byte Order { get; }
}
