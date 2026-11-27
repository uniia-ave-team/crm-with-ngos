using Crm.Domain.Attributes;
using Crm.Domain.Entities.Base;
using Crm.Domain.Interfaces.Entities;
using Crm.Domain.ValueObjects;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents a reusable blueprint (template) for creating custom tables.
/// Stores its column blueprint directly as a JSONB collection of <see cref="TemplateFieldDefinition"/> value objects.
/// </summary>
[Auditable]
public class TableTemplate : BaseEntity, IAuditableEntity, IAuthorTrackedEntity
{
    /// <summary>
    /// Gets or sets the display name of the template (e.g., "Standard Donor", "Beneficiary Base").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a brief explanation of the template's purpose and intended use cases.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this template is a global system preset provided by default.
    /// System presets cannot be deleted or modified by regular users.
    /// </summary>
    public bool IsSystemPreset { get; set; }

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when this template was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when this template was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the optional unique identifier of the user who created this template.
    /// </summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the creator <see cref="User"/>.
    /// </summary>
    public User? Creator { get; set; }

    /// <summary>
    /// Gets or sets the optional unique identifier of the user who last updated this template.
    /// </summary>
    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the updater <see cref="User"/>.
    /// </summary>
    public User? Updater { get; set; }

    /// <summary>
    /// Gets or sets the ordered column blueprint definitions stored as JSONB on this template.
    /// </summary>
    public List<TemplateFieldDefinition> Fields { get; set; } = [];
}
