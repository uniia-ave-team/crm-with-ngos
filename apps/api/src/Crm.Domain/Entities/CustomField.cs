using Crm.Domain.Enums;
using Crm.Domain.Interfaces;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents a specific field (column) definition inside an NGO's <see cref="CustomTable"/>.
/// While initially copied from a <see cref="TemplateField"/>, NGOs can modify these or add entirely new custom fields.
/// </summary>
public class CustomField : BaseEntity, IEntityField
{
    /// <summary>
    /// Gets or sets the display name of the field as seen by the NGO users.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exact dictionary key used to map values inside the <see cref="CustomRecord.Data"/> JSON payload.
    /// Must be unique within the context of its parent <see cref="CustomTable"/>.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the data type of the field, ensuring proper validation and UI rendering.
    /// </summary>
    public FieldDataType DataType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the field must be populated to save a valid record in this table.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets the display order or position sequence of the field.
    /// </summary>
    public byte Order { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the custom table this field belongs to.
    /// </summary>
    public Guid CustomTableId { get; set; }

    /// <summary>
    /// Gets or sets the parent custom table.
    /// </summary>
    public CustomTable CustomTable { get; set; }
}
