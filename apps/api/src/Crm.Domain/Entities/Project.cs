using Crm.Domain.Attributes;
using Crm.Domain.Entities.Base;
using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Entities;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents a project within the NGO CRM Task Tracker (Planner, Board, Tables).
/// </summary>
[Auditable]
public class Project : BaseEntity, IAuditableEntity, IAuthorTrackedEntity, ISoftDeletableEntity
{
    /// <summary>
    /// Gets or sets the title of the project.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional description of the project.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the combined view and edit visibility level of the project.
    /// </summary>
    public ProjectVisibility Visibility { get; set; }

    /// <summary>
    /// Gets or sets the UTC+0 start date/time of the project.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Gets or sets the optional UTC+0 end date/time of the project.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when the project was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when the project was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the optional unique identifier of the user who created the project.
    /// Set to <c>null</c> if the creator user is deleted.
    /// </summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the creator <see cref="User"/>.
    /// </summary>
    public User? Creator { get; set; }

    /// <summary>
    /// Gets or sets the optional unique identifier of the user who last updated the project.
    /// Set to <c>null</c> if the updater user is deleted.
    /// </summary>
    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the updater <see cref="User"/>.
    /// </summary>
    public User? Updater { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the project has been soft-deleted.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the optional UTC+0 timestamp when the project was soft-deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Gets the collection of project members with project-specific roles and claims.
    /// </summary>
    public ICollection<ProjectMember> Members { get; init; } = [];

    /// <summary>
    /// Gets the collection of board topics/sections belonging to this project.
    /// </summary>
    public ICollection<Topic> Topics { get; init; } = [];

    /// <summary>
    /// Gets the collection of tasks belonging to this project.
    /// </summary>
    public ICollection<ProjectTask> Tasks { get; init; } = [];

    /// <summary>
    /// Gets the collection of project-scoped tags.
    /// </summary>
    public ICollection<Tag> Tags { get; init; } = [];
}
