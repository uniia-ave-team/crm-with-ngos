using Crm.Domain.Enums;
using Crm.Domain.Interfaces;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents a predefined field (column) definition within a reusable <see cref="TableTemplate"/>.
/// When a template is instantiated, these fields are copied to become <see cref="CustomField"/>s.
/// </summary>
public class TemplateField : BaseEntity, IEntityField
{
    /// <summary>
    /// Gets or sets the human-readable name of the field (e.g., "Passport Number").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique, machine-readable key used for internal mapping and JSON serialization.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the designated data type, dictating the validation rules and UI representation.
    /// </summary>
    public FieldDataType DataType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether for this field is mandatory when creating a record.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets the display order or position sequence of the field.
    /// </summary>
    public byte Order { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the parent template.
    /// </summary>
    public Guid TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the parent template to which this field belongs.
    /// </summary>
    public TableTemplate Template { get; set; }
}
