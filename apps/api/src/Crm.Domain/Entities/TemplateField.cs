using Crm.Domain.Attributes;
using Crm.Domain.Entities.Base;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents a predefined field (column) definition within a reusable <see cref="TableTemplate"/>.
/// When a template is instantiated, these fields are copied to become <see cref="CustomField"/>s.
/// </summary>
[Auditable]
public class TemplateField : FieldDefinitionBase
{
    /// <summary>
    /// Gets or sets the unique identifier of the parent template.
    /// </summary>
    public Guid TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the parent template to which this field belongs.
    /// </summary>
    public TableTemplate Template { get; set; } = null!;
}
