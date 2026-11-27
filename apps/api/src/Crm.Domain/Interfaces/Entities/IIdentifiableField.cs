namespace Crm.Domain.Interfaces.Entities;

/// <summary>
/// Represents an incoming field update definition carrying an optional database identifier
/// to match an existing field (preserving its immutable key) or create a new field when <see cref="Id"/> is <see langword="null"/>.
/// </summary>
public interface IIdentifiableField : IFieldDefinition
{
    /// <summary>
    /// Gets the unique identifier of an existing field to update, or <see langword="null"/> to create a new field.
    /// </summary>
    Guid? Id { get; }
}
