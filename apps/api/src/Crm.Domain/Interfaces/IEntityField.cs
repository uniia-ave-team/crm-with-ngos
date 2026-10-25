namespace Crm.Domain.Interfaces;

/// <summary>
/// Represents a persisted field domain entity that strictly requires a non-nullable unique identifier.
/// </summary>
public interface IEntityField : IIdentifiableField
{
    /// <summary>
    /// Gets the mandatory unique identifier for the persisted field entity.
    /// </summary>
    new Guid Id { get; }

    /// <summary>
    /// Gets the default bridge implementation mapping non-nullable <see cref="Id"/> to <see cref="IIdentifiableField.Id"/>.
    /// </summary>
    Guid? IIdentifiableField.Id => Id == Guid.Empty ? null : Id;
}
