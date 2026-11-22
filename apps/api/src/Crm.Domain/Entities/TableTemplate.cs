using Crm.Domain.Attributes;
using Crm.Domain.Entities.Base;
using Crm.Domain.Interfaces.Entities;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents a reusable blueprint (template) for creating custom tables.
/// This entity defines the schema but does not store any actual user data.
/// It acts as a predefined structure that NGOs can instantiate into actual <see cref="CustomTable"/>s.
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
    /// System presets typically cannot be deleted or modified by regular users.
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
    /// Gets the collection of field definitions that make up the schema of this template.
    /// </summary>
    public ICollection<TemplateField> Fields { get; init; } = [];
}
