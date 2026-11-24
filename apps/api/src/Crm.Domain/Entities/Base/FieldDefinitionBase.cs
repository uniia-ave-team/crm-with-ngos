using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Entities;

namespace Crm.Domain.Entities.Base;

/// <summary>
/// Serves as the shared abstract base class for persisted dynamic field schema definitions
/// (<see cref="CustomField"/> and <see cref="TemplateField"/>).
/// </summary>
public abstract class FieldDefinitionBase : BaseEntity, IEntityField, IAuditableEntity
{
    /// <summary>
    /// Gets or sets the human-readable display name of the field.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique, machine-readable key used for internal mapping and JSON serialization.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the designated data type, dictating validation rules and UI representation.
    /// </summary>
    public FieldDataType DataType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this field is mandatory when creating a record.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets the display order or position sequence of the field.
    /// </summary>
    public byte Order { get; set; }

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when this field definition was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when this field definition was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
