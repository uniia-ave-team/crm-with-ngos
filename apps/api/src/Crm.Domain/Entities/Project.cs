using Crm.Domain.Enums;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents a project within the NGO CRM Task Tracker (Planner, Board, Tables).
/// </summary>
public class Project : BaseEntity
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
    /// Gets or sets the UTC start date/time of the project.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Gets or sets the optional UTC end date/time of the project.
    /// </summary>
    public DateTime? EndDate { get; set; }

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
    /// Gets or sets the collection of project members with project-specific roles and claims.
    /// </summary>
    public ICollection<ProjectMember> Members { get; set; } = [];

    /// <summary>
    /// Gets or sets the collection of board topics/sections belonging to this project.
    /// </summary>
    public ICollection<Topic> Topics { get; set; } = [];

    /// <summary>
    /// Gets or sets the collection of tasks belonging to this project.
    /// </summary>
    public ICollection<ProjectTask> Tasks { get; set; } = [];

    /// <summary>
    /// Gets or sets the collection of project-scoped tags.
    /// </summary>
    public ICollection<Tag> Tags { get; set; } = [];
}
