using Crm.Domain.Attributes;
using Crm.Domain.Interfaces.Entities;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents the single system-wide scheduled maintenance notice stored in the database.
/// </summary>
[Auditable]
public class MaintenanceNotice : IAuditableEntity, IAuthorTrackedEntity
{
    /// <summary>
    /// Fixed primary key enforcing that at most one maintenance notice record exists in the database.
    /// </summary>
    public const int SingletonId = 1;

    /// <summary>
    /// Gets the singleton record identifier (always <see cref="SingletonId"/>).
    /// </summary>
    public int Id { get; private set; } = SingletonId;

    /// <summary>
    /// Gets or sets the announcement message displayed to users.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when the scheduled maintenance window starts.
    /// </summary>
    public DateTime StartsAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when the scheduled maintenance window ends.
    /// </summary>
    public DateTime EndsAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when this notice was initially created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when this notice was last created or updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the optional unique identifier of the user who created the maintenance notice.
    /// </summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the creator <see cref="User"/>.
    /// </summary>
    public User? Creator { get; set; }

    /// <summary>
    /// Gets or sets the optional unique identifier of the user who last updated the maintenance notice.
    /// </summary>
    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the updater <see cref="User"/>.
    /// </summary>
    public User? Updater { get; set; }
}
