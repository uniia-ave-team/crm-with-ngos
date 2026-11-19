using System.Text.Json.Nodes;
using Crm.Domain.Common;
using Crm.Domain.Enums;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents a task within a project in the NGO CRM Task Tracker.
/// </summary>
public class ProjectTask : BaseEntity
{
    /// <summary>
    /// Gets or sets the foreign key identifier of the parent <see cref="Project"/>.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the parent <see cref="Project"/>.
    /// </summary>
    public Project Project { get; set; }

    /// <summary>
    /// Gets or sets the foreign key identifier of the required <see cref="Topic"/> (board column/section).
    /// </summary>
    public Guid TopicId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the associated <see cref="Topic"/>.
    /// </summary>
    public Topic Topic { get; set; }

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
    /// Gets or sets the UTC start date/time of the task.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Gets or sets the optional UTC end/completion date/time of the task.
    /// Automatically populated with the current timestamp if null when status transitions to <see cref="ProjectTaskStatus.Completed"/>.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets a value indicating whether <see cref="EndDate"/> was automatically populated upon transitioning to
    /// <see cref="ProjectTaskStatus.Completed"/> rather than explicitly supplied by a user.
    /// </summary>
    public bool EndDateWasAuto { get; internal set; }

    /// <summary>
    /// Gets or sets the collection of external links attached to the task.
    /// </summary>
    public List<TaskExternalLink> ExternalLinks { get; set; } = [];

    /// <summary>
    /// Gets or sets the flexible key-value metadata payload attached to the task.
    /// </summary>
    public JsonObject Metadata { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC timestamp when the task was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the task's core properties, status, tags, or assignees were last modified.
    /// Updated when task fields/status change or when tags/assignees are added or removed; not updated by comments or attachments.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the users assigned to this task.
    /// </summary>
    public ICollection<User> AssignedUsers { get; set; } = [];

    /// <summary>
    /// Gets or sets the tags associated with this task.
    /// </summary>
    public ICollection<Tag> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets the comments posted on this task.
    /// </summary>
    public ICollection<TaskComment> Comments { get; set; } = [];

    /// <summary>
    /// Gets or sets the file attachments associated with this task.
    /// </summary>
    public ICollection<TaskAttachment> Attachments { get; set; } = [];
}
