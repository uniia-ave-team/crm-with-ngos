namespace Crm.Domain.Interfaces;

/// <summary>
/// Represents a field schema associated with a unique database identifier.
/// </summary>
public interface IIdentifiableField : IFieldSchema
{
    /// <summary>
    /// Gets the unique identifier for the field schema entity, or <see langword="null"/> if the field is not persisted.
    /// </summary>
    Guid? Id { get; }
}
