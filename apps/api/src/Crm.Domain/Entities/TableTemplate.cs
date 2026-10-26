namespace Crm.Domain.Entities;

/// <summary>
/// Represents a reusable blueprint (template) for creating custom tables.
/// This entity defines the schema but does not store any actual user data.
/// It acts as a predefined structure that NGOs can instantiate into actual <see cref="CustomTable"/>s.
/// </summary>
public class TableTemplate : BaseEntity
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
    /// Gets or sets the collection of field definitions that make up the schema of this template.
    /// </summary>
    public ICollection<TemplateField> Fields { get; set; } = [];
}
