using Crm.Domain.Attributes;
using Crm.Domain.Enums;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents a direct cross-table relation linking a <see cref="Project"/> and a <see cref="User"/>,
/// storing a free-text functional role title and a project-specific hierarchical permission claim.
/// </summary>
[Auditable]
public class ProjectMember
{
    /// <summary>
    /// Gets or sets the foreign key identifier of the associated <see cref="Project"/>.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the parent <see cref="Project"/>.
    /// </summary>
    public Project Project { get; set; } = null!;

    /// <summary>
    /// Gets or sets the foreign key identifier of the associated <see cref="User"/>.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the assigned <see cref="User"/>.
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Gets or sets the free-text functional role description specific to this user in this project
    /// (e.g., "Developer", "SMM Manager", "Editor").
    /// </summary>
    public string RoleTitle { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hierarchical project-specific permission claim assigned to the user on this project.
    /// Higher claims implicitly include all permissions granted by lower claims.
    /// </summary>
    public ProjectClaim Claim { get; set; } = ProjectClaim.View;

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when the user was assigned to the project.
    /// </summary>
    public DateTime JoinedAt { get; set; }
}
