namespace Crm.Domain.Interfaces.Entities;

/// <summary>
/// Represents the user-editable properties of a dynamic field definition
/// (<see cref="Name"/>, <see cref="IsRequired"/>, and display <see cref="IOrderedEntity.Order"/>).
/// Persisted field entities use their <see cref="IEntity.Id"/> directly as the JSONB storage key.
/// </summary>
public interface IFieldDefinition : IOrderedEntity
{
    /// <summary>
    /// Gets the human-readable display name of the field.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a value indicating whether a non-null string value is required for this field.
    /// </summary>
    bool IsRequired { get; }
}
