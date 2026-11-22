namespace Crm.Domain.Entities;

/// <summary>
/// Represents the single system-wide scheduled maintenance notice stored in the database.
/// </summary>
public class MaintenanceNotice
{
    /// <summary>
    /// Fixed primary key enforcing that at most one maintenance notice record exists in the database.
    /// </summary>
    public const int SingletonId = 1;

    /// <summary>
    /// Gets or sets the singleton record identifier (always <see cref="SingletonId"/>).
    /// </summary>
    public int Id { get; set; } = SingletonId;

    /// <summary>
    /// Gets or sets the announcement message displayed to users.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the scheduled maintenance window starts.
    /// </summary>
    public DateTime StartsAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the scheduled maintenance window ends.
    /// </summary>
    public DateTime EndsAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when this notice was last created or updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
