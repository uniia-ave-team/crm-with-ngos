using System.Text.Json.Nodes;
using Crm.Domain.Attributes;
using Crm.Domain.Entities.Base;
using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Entities;
using Crm.Domain.ValueObjects;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents a task within a project in the NGO CRM Task Tracker.
/// </summary>
[Auditable]
public class ProjectTask : BaseEntity, IAuditableEntity, IAuthorTrackedEntity, ISoftDeletableEntity
{
    /// <summary>
    /// Gets or sets the foreign key identifier of the parent <see cref="Project"/>.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the parent <see cref="Project"/>.
    /// </summary>
    public Project Project { get; set; } = null!;

    /// <summary>
    /// Gets or sets the foreign key identifier of the required <see cref="Topic"/> (board column/section).
    /// </summary>
    public Guid TopicId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the associated <see cref="Topic"/>.
    /// </summary>
    public Topic Topic { get; set; } = null!;

    /// <summary>
    /// Gets or sets the title of the task.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Markdown-enabled description of the task.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the predefined execution status of the task.
    /// </summary>
    public ProjectTaskStatus Status { get; set; } = ProjectTaskStatus.ToDo;

    /// <summary>
    /// Gets or sets the predefined priority level of the task.
    /// </summary>
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    /// <summary>
    /// Gets or sets the UTC+0 start date/time of the task.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Gets or sets the optional UTC+0 end/completion date/time of the task.
    /// Automatically populated with the current timestamp if null when status transitions to <see cref="ProjectTaskStatus.Completed"/>.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets a value indicating whether <see cref="EndDate"/> was automatically populated upon transitioning to
    /// <see cref="ProjectTaskStatus.Completed"/> rather than explicitly supplied by a user.
    /// </summary>
    public bool EndDateWasAuto { get; internal set; }

    /// <summary>
    /// Gets the collection of external links attached to the task.
    /// </summary>
    public ICollection<TaskExternalLink> ExternalLinks { get; init; } = [];

    /// <summary>
    /// Gets or sets the flexible key-value metadata payload attached to the task.
    /// </summary>
    public JsonObject Metadata { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when the task was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when the task's core properties, status, tags, or assignees were last modified.
    /// Updated when task fields/status change or when tags/assignees are added or removed; not updated by comments or attachments.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the optional unique identifier of the user who created the task.
    /// </summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the creator <see cref="User"/>.
    /// </summary>
    public User? Creator { get; set; }

    /// <summary>
    /// Gets or sets the optional unique identifier of the user who last updated the task.
    /// </summary>
    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the updater <see cref="User"/>.
    /// </summary>
    public User? Updater { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the task has been soft-deleted.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the optional UTC+0 timestamp when the task was soft-deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Gets the users assigned to this task.
    /// </summary>
    public ICollection<User> AssignedUsers { get; init; } = [];

    /// <summary>
    /// Gets the tags associated with this task.
    /// </summary>
    public ICollection<Tag> Tags { get; init; } = [];

    /// <summary>
    /// Gets the comments posted on this task.
    /// </summary>
    public ICollection<TaskComment> Comments { get; init; } = [];

    /// <summary>
    /// Gets the file attachments associated with this task.
    /// </summary>
    public ICollection<TaskAttachment> Attachments { get; init; } = [];
}
