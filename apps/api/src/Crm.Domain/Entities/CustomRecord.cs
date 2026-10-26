using System.Text.Json.Nodes;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents a single row of dynamic data within a <see cref="CustomTable"/>.
/// Values are stored flexibly using a JSON column mapping, providing NoSQL-like agility within a relational database.
/// </summary>
public class CustomRecord : BaseEntity
{
    /// <summary>
    /// Gets or sets the identifier of the table where this record resides.
    /// </summary>
    public Guid CustomTableId { get; set; }

    /// <summary>
    /// Gets or sets the parent table instance containing this record.
    /// </summary>
    public CustomTable CustomTable { get; set; }

    /// <summary>
    /// Gets or sets the dynamic payload containing the actual field values.
    /// The dictionary keys must correspond to the <see cref="CustomField.Key"/> properties defined in the table's schema.
    /// </summary>
    public JsonObject Data { get; set; } = [];
}
