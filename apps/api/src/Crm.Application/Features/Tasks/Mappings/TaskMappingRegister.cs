using Crm.Application.Dtos.Task;
using Crm.Application.Dtos.Task.Commands;
using Crm.Application.Dtos.Task.Queries;
using Crm.Domain.Common;
using Crm.Domain.Consts.Entities.GeneralConstants;
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
                src => src.Filter != null && string.Equals(src.Filter.Trim(), TaskConstants.MineFilterValue, StringComparison.OrdinalIgnoreCase));

        config.NewConfig<TaskExternalLink, TaskExternalLinkDto>();

        config.NewConfig<User, TaskAssigneeDto>()
            .MapToConstructor(true)
            .Map(dest => dest.AvatarUrl, src => src.Avatar);

        config.NewConfig<TaskComment, TaskCommentDto>()
            .MapToConstructor(true)
            .Map(dest => dest.AuthorFirstName, src => src.User != null ? src.User.FirstName : null)
            .Map(dest => dest.AuthorLastName, src => src.User != null ? src.User.LastName : null)
            .Map(dest => dest.AuthorPatronymic, src => src.User != null ? src.User.Patronymic : null)
            .Map(dest => dest.AuthorAvatarUrl, src => src.User != null ? src.User.Avatar : null);

        config.NewConfig<TaskAttachment, TaskAttachmentDto>()
            .MapToConstructor(true)
            .Map(dest => dest.Url, _ => (string?)null)
            .Map(dest => dest.StorageKey, src => src.StoredFileName);

        config.NewConfig<ProjectTask, TaskSummaryDto>()
            .MapToConstructor(true)
            .Map(dest => dest.TopicTitle, src => src.Topic.Title)
            .Map(dest => dest.UnreadCommentsCount, _ => UnreadCountPlaceholder)
            .Map(dest => dest.AttachmentsCount, src => src.Attachments.Count())
            .Map(dest => dest.AssignedUsers, src => src.AssignedUsers.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ThenBy(u => u.Id))
            .Map(dest => dest.Tags, src => src.Tags.OrderBy(t => t.Title));

        config.NewConfig<ProjectTask, TaskDto>()
            .MapToConstructor(true)
            .Map(dest => dest.TopicTitle, src => src.Topic.Title)
            .Map(dest => dest.UnreadCommentsCount, _ => UnreadCountPlaceholder)
            .Map(dest => dest.AssignedUsers, src => src.AssignedUsers.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ThenBy(u => u.Id))
            .Map(dest => dest.Tags, src => src.Tags.OrderBy(t => t.Title))
            .Map(dest => dest.Attachments, src => src.Attachments.OrderByDescending(a => a.CreatedAt));
    }
}
