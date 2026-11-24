using System.Text.Json.Nodes;
using Crm.Domain.Attributes;
using Crm.Domain.Entities.Base;
using Crm.Domain.Interfaces.Entities;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents a single row of dynamic data within a <see cref="CustomTable"/>.
/// Values are stored flexibly using a JSON column mapping, providing NoSQL-like agility within a relational database.
/// </summary>
[Auditable]
public class CustomRecord : BaseEntity, IAuditableEntity, IAuthorTrackedEntity
{
    /// <summary>
    /// Gets or sets the identifier of the table where this record resides.
    /// </summary>
    public Guid CustomTableId { get; set; }

    /// <summary>
    /// Gets or sets the parent table instance containing this record.
    /// </summary>
    public CustomTable CustomTable { get; set; } = null!;

    /// <summary>
    /// Gets or sets the dynamic payload containing the actual field values.
    /// The dictionary keys must correspond to the <see cref="FieldDefinitionBase.Key"/> properties defined in the table's schema.
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
