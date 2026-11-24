using Crm.Domain.Attributes;
using Crm.Domain.Entities.Base;
using Crm.Domain.Interfaces.Entities;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents an actual, instantiated custom table belonging to a specific Non-Governmental Organization (NGO).
/// It contains the organization's unique schema (fields) and the actual data rows (records).
/// </summary>
[Auditable]
public class CustomTable : BaseEntity, IAuditableEntity, IAuthorTrackedEntity, ISoftDeletableEntity
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
    public Ngo Ngo { get; set; } = null!;

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when this table was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when this table was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the optional unique identifier of the user who created this table.
    /// </summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the creator <see cref="User"/>.
    /// </summary>
    public User? Creator { get; set; }

    /// <summary>
    /// Gets or sets the optional unique identifier of the user who last updated this table.
    /// </summary>
    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the updater <see cref="User"/>.
    /// </summary>
    public User? Updater { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this table has been soft-deleted.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the optional UTC+0 timestamp when this table was soft-deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Gets the collection of custom fields (schema definition) specific to this table.
    /// </summary>
    public ICollection<CustomField> Fields { get; init; } = [];

    /// <summary>
    /// Gets the collection of actual data records stored within this table.
    /// </summary>
    public ICollection<CustomRecord> Records { get; init; } = [];
}
