using Crm.Domain.Interfaces.Entities;

namespace Crm.Domain.Entities.Base;

/// <summary>
/// Base class for persisted dynamic field schema definitions (<see cref="CustomField"/> and <see cref="UserCustomField"/>),
/// sharing display name, requiredness, display order, audit timestamps, and an immutable <see cref="BaseEntity.Id"/>
/// used directly as the JSONB property key inside record and user payloads.
/// </summary>
public abstract class FieldDefinitionBase : BaseEntity, IFieldDefinition, IAuditableEntity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FieldDefinitionBase"/> class
    /// with a freshly generated Version 7 GUID identifier.
    /// </summary>
    protected FieldDefinitionBase()
    {
        Id = Guid.CreateVersion7();
    }

    /// <summary>
    /// Gets or sets the human-readable display name of the field (e.g., "Organization Name").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether a non-null string value is required for this field.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets the zero-based display order of the field.
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
