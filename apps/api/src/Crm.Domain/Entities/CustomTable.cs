namespace Crm.Domain.Entities;

/// <summary>
/// Represents an actual, instantiated custom table belonging to a specific Non-Governmental Organization (NGO).
/// It contains the organization's unique schema (fields) and the actual data rows (records).
/// </summary>
public class CustomTable : BaseEntity
{
    /// <summary>
    /// Gets or sets the name of the custom table as defined by the NGO (e.g., "VIP Donors 2026").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional description for the table's specific purpose within the NGO.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the optional identifier of the <see cref="TableTemplate"/> this table was originally created from.
    /// This is useful for analytics, grouping, or checking for upstream template updates.
    /// </summary>
    public Guid? SourceTemplateId { get; set; }

    /// <summary>
    /// Gets or sets the source template this table was instantiated from, if applicable.
    /// </summary>
    public TableTemplate? SourceTemplate { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the NGO that owns this table.
    /// </summary>
    public Guid NgoId { get; set; }

    /// <summary>
    /// Gets or sets the NGO instance that owns and manages this table.
    /// </summary>
    public Ngo Ngo { get; set; }

    /// <summary>
    /// Gets or sets the collection of custom fields (schema definition) specific to this table.
    /// </summary>
    public ICollection<CustomField> Fields { get; set; } = [];

    /// <summary>
    /// Gets or sets the collection of actual data records stored within this table.
    /// </summary>
    public ICollection<CustomRecord> Records { get; set; } = [];
}
