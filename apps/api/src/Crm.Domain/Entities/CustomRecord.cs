using System.Text.Json.Nodes;
using Crm.Domain.Attributes;
using Crm.Domain.Entities.Base;
using Crm.Domain.Interfaces.Entities;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents a single dynamic data row (record) stored within a specific <see cref="CustomTable"/>.
/// Field values are stored as string-only entries inside the JSONB <see cref="Data"/> column,
/// while manual row order is maintained via lexicographical fractional indexing (<see cref="Position"/>).
/// </summary>
[Auditable]
public class CustomRecord : BaseEntity, IAuditableEntity, IAuthorTrackedEntity
{
    /// <summary>
    /// Gets or sets the unique identifier of the <see cref="CustomTable"/> that owns this record.
    /// </summary>
    public Guid CustomTableId { get; set; }

    /// <summary>
    /// Gets or sets the parent custom table to which this record belongs.
    /// </summary>
    public CustomTable CustomTable { get; set; } = null!;

    /// <summary>
    /// Gets or sets the lexicographical fractional-indexing rank defining this row's manual position within its table.
    /// Decorated with <see cref="AuditIgnoreAttribute"/> so drag-and-drop reordering does not emit noisy field diffs.
    /// </summary>
    [AuditIgnore]
    public string Position { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the dynamic JSONB document containing the actual string cell values for this record,
    /// keyed by each <see cref="BaseEntity.Id"/> string defined in the parent table's schema.
    /// </summary>
    public JsonObject Data { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when this record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when this record was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the optional unique identifier of the user who created this record.
    /// </summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the creator <see cref="User"/>.
    /// </summary>
    public User? Creator { get; set; }

    /// <summary>
    /// Gets or sets the optional unique identifier of the user who last updated this record.
    /// </summary>
    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the updater <see cref="User"/>.
    /// </summary>
    public User? Updater { get; set; }
}
