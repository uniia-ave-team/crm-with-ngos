using System.Text.Json.Nodes;
using Crm.Application.Dtos.Projects;
using Crm.Domain.Enums;

namespace Crm.Application.Dtos.Tasks;

/// <summary>
/// Represents detailed information about a project task, including its status, priority, dates,
/// external links, metadata, assignees, tags, attachments, and unread comments count.
/// </summary>
/// <param name="Id">The unique identifier of the task.</param>
/// <param name="ProjectId">The unique identifier of the parent project.</param>
/// <param name="TopicId">The unique identifier of the board topic/section the task belongs to.</param>
/// <param name="TopicTitle">The display title of the board topic/section the task belongs to.</param>
/// <param name="Title">The display title of the task.</param>
/// <param name="Description">The optional Markdown-enabled description of the task.</param>
/// <param name="Status">The current execution status of the task.</param>
/// <param name="Priority">The priority level of the task.</param>
/// <param name="StartDate">The UTC start date/time of the task.</param>
/// <param name="EndDate">The optional UTC end/completion date/time of the task.</param>
/// <param name="EndDateWasAuto">Indicates whether <paramref name="EndDate"/> was automatically populated on completion rather than explicitly set.</param>
/// <param name="UnreadCommentsCount">The number of comments on this task that are unread by the current user.</param>
/// <param name="AssignedUsers">The list of users assigned to the task.</param>
/// <param name="Tags">The list of project-scoped tags associated with the task.</param>
/// <param name="Attachments">The list of file attachments associated with the task.</param>
/// <param name="ExternalLinks">The list of external links associated with the task.</param>
/// <param name="Metadata">The flexible JSON key-value metadata attached to the task.</param>
/// <param name="CreatedAt">The UTC timestamp when the task was created.</param>
/// <param name="UpdatedAt">The UTC timestamp when the task was last updated.</param>
public record TaskDto(
    Guid Id,
    Guid ProjectId,
    Guid TopicId,
    string TopicTitle,
    string Title,
    string? Description,
    ProjectTaskStatus Status,
    TaskPriority Priority,
    DateTime StartDate,
    DateTime? EndDate,
    bool EndDateWasAuto,
    int UnreadCommentsCount,
    IReadOnlyList<TaskAssigneeDto> AssignedUsers,
    IReadOnlyList<TagDto> Tags,
    IReadOnlyList<TaskAttachmentDto> Attachments,
    IReadOnlyList<TaskExternalLinkDto> ExternalLinks,
    JsonObject Metadata,
    DateTime CreatedAt,
    DateTime UpdatedAt);
