using Crm.Domain.Attributes;
using Crm.Domain.Entities.Base;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents a specific field (column) definition inside an NGO's <see cref="CustomTable"/>.
/// While initially copied from a <see cref="TemplateField"/>, NGOs can modify these or add entirely new custom fields.
/// </summary>
[Auditable]
public class CustomField : FieldDefinitionBase
{
    /// <summary>
    /// Gets or sets the identifier of the custom table this field belongs to.
    /// </summary>
    public Guid CustomTableId { get; set; }

    /// <summary>
    /// Gets or sets the parent custom table.
    /// </summary>
    public CustomTable CustomTable { get; set; } = null!;
}
