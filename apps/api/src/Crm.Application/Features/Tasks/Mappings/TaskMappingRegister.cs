using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Projects;
using Crm.Application.Dtos.Tasks;
using Crm.Application.Features.Tasks.Commands;
using Crm.Application.Features.Tasks.Queries;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Mapster;

namespace Crm.Application.Features.Tasks.Mappings;

/// <summary>
/// Registers Mapster mapping configurations for SQL-projected task, comment, and attachment DTOs
/// as well as command and filter mappings.
/// </summary>
public sealed class TaskMappingRegister : IRegister
{
    private const int UnreadCountPlaceholder = 0;

    /// <inheritdoc />
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<CreateTaskCommentCommand, TaskComment>()
            .Ignore(dest => dest.Id)
            .Map(dest => dest.Content, src => src.Content.Trim());

        config.NewConfig<GetProjectTasksQuery, ProjectTaskFilter>()
            .Map(
                dest => dest.IsMine,
                src => src.Filter != null && string.Equals(src.Filter.Trim(), TaskFilterConstants.MineFilterValue, StringComparison.OrdinalIgnoreCase));

        config.NewConfig<TaskComment, TaskCommentDto>()
            .MapWith(src => new TaskCommentDto(
                src.Id,
                src.TaskId,
                src.UserId,
                src.User != null ? src.User.FirstName : null,
                src.User != null ? src.User.LastName : null,
                src.User != null ? src.User.Patronymic : null,
                src.User != null ? src.User.Avatar : null,
                src.Content,
                src.CreatedAt,
                src.UpdatedAt));

        config.NewConfig<TaskAttachment, TaskAttachmentDto>()
            .MapWith(src => new TaskAttachmentDto(
                src.Id,
                src.TaskId,
                src.UploadedBy,
                src.OriginalFileName,
                src.ContentType,
                src.FileSize,
                src.StoredFileName,
                src.CreatedAt));

        config.NewConfig<ProjectTask, TaskSummaryDto>()
            .MapWith(src => new TaskSummaryDto(
                src.Id,
                src.ProjectId,
                src.TopicId,
                src.Topic.Title,
                src.Title,
                src.Description,
                src.Status,
                src.Priority,
                src.StartDate,
                src.EndDate,
                src.EndDateWasAuto,
                UnreadCountPlaceholder,
                src.Attachments.Count(),
                src.AssignedUsers
                    .OrderBy(u => u.LastName)
                    .ThenBy(u => u.FirstName)
                    .ThenBy(u => u.Id)
                    .Select(u => new TaskAssigneeDto(
                        u.Id,
                        u.FirstName,
                        u.LastName,
                        u.Patronymic,
                        u.Avatar))
                    .ToList(),
                src.Tags
                    .OrderBy(t => t.Title)
                    .ThenBy(t => t.Id)
                    .Select(t => new TagDto(
                        t.Id,
                        t.ProjectId,
                        t.Title))
                    .ToList(),
                src.CreatedAt,
                src.UpdatedAt));

        config.NewConfig<ProjectTask, TaskDto>()
            .MapWith(src => new TaskDto(
                src.Id,
                src.ProjectId,
                src.TopicId,
                src.Topic.Title,
                src.Title,
                src.Description,
                src.Status,
                src.Priority,
                src.StartDate,
                src.EndDate,
                src.EndDateWasAuto,
                UnreadCountPlaceholder,
                src.AssignedUsers
                    .OrderBy(u => u.LastName)
                    .ThenBy(u => u.FirstName)
                    .ThenBy(u => u.Id)
                    .Select(u => new TaskAssigneeDto(
                        u.Id,
                        u.FirstName,
                        u.LastName,
                        u.Patronymic,
                        u.Avatar))
                    .ToList(),
                src.Tags
                    .OrderBy(t => t.Title)
                    .ThenBy(t => t.Id)
                    .Select(t => new TagDto(
                        t.Id,
                        t.ProjectId,
                        t.Title))
                    .ToList(),
                src.Attachments
                    .OrderByDescending(a => a.CreatedAt)
                    .ThenBy(a => a.Id)
                    .Select(a => new TaskAttachmentDto(
                        a.Id,
                        a.TaskId,
                        a.UploadedBy,
                        a.OriginalFileName,
                        a.ContentType,
                        a.FileSize,
                        a.StoredFileName,
                        a.CreatedAt))
                    .ToList(),
                src.ExternalLinks
                    .Select(l => new TaskExternalLinkDto(
                        l.Title,
                        l.Url))
                    .ToList(),
                src.Metadata,
                src.CreatedAt,
                src.UpdatedAt));
    }
}
