using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Tasks;
using Crm.Application.Features.Users.Extensions;
using Crm.Application.Interfaces;

namespace Crm.Application.Features.Tasks.Extensions;

/// <summary>
/// Provides URL enrichment and unread-count hydration helpers for projected task DTOs.
/// </summary>
public static class TaskDtoExtensions
{
    /// <summary>
    /// Resolves the download URL for a <see cref="TaskAttachmentDto"/> from its projected <see cref="TaskAttachmentDto.Url"/> key.
    /// </summary>
    /// <param name="attachment">The projected task attachment DTO to enrich.</param>
    /// <param name="projectId">The unique identifier of the parent project.</param>
    /// <param name="fileUrlProvider">The file URL provider used to resolve the download URL.</param>
    /// <returns>A copy of <paramref name="attachment"/> with its resolved download URL.</returns>
    public static TaskAttachmentDto EnrichUrl(
        this TaskAttachmentDto attachment,
        Guid projectId,
        IFileUrlProvider fileUrlProvider)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        ArgumentNullException.ThrowIfNull(fileUrlProvider);

        if (string.IsNullOrWhiteSpace(attachment.Url))
        {
            return attachment with { Url = null };
        }

        string route = FileRouteConstants.TaskAttachmentFile(projectId, attachment.TaskId, attachment.Id);
        return attachment with
        {
            Url = fileUrlProvider.GetFileUrl(attachment.Url, route),
        };
    }

    /// <summary>
    /// Resolves download URLs for a list of <see cref="TaskAttachmentDto"/> items.
    /// </summary>
    /// <param name="attachments">The sequence of projected task attachment DTOs to enrich.</param>
    /// <param name="projectId">The unique identifier of the parent project.</param>
    /// <param name="fileUrlProvider">The file URL provider used to resolve download URLs.</param>
    /// <returns>A list of <see cref="TaskAttachmentDto"/> items with resolved download URLs.</returns>
    public static List<TaskAttachmentDto> EnrichUrls(
        this IEnumerable<TaskAttachmentDto> attachments,
        Guid projectId,
        IFileUrlProvider fileUrlProvider)
    {
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentNullException.ThrowIfNull(fileUrlProvider);

        return [.. attachments.Select(a => a.EnrichUrl(projectId, fileUrlProvider))];
    }

    /// <summary>
    /// Resolves author avatar URLs for a list of <see cref="TaskCommentDto"/> items.
    /// </summary>
    /// <param name="comments">The sequence of projected task comment DTOs to enrich.</param>
    /// <param name="fileUrlProvider">The file URL provider used to resolve author avatar URLs.</param>
    /// <param name="currentUserId">The unique identifier of the current user.</param>
    /// <returns>A list of <see cref="TaskCommentDto"/> items with resolved author avatar URLs.</returns>
    public static List<TaskCommentDto> EnrichUrls(
        this IEnumerable<TaskCommentDto> comments,
        IFileUrlProvider fileUrlProvider,
        Guid currentUserId)
    {
        ArgumentNullException.ThrowIfNull(comments);
        ArgumentNullException.ThrowIfNull(fileUrlProvider);

        return
        [
            .. comments.Select(comment => comment with
            {
                AuthorAvatarUrl = comment.UserId is { } authorId
                    ? fileUrlProvider.ResolveUserAvatarUrl(
                        comment.AuthorAvatarUrl,
                        authorId,
                        authorId == currentUserId)
                    : null,
            }),
        ];
    }

    /// <summary>
    /// Resolves assignee avatars, attachment URLs, and the unread comment count for a <see cref="TaskDto"/>.
    /// </summary>
    /// <param name="task">The projected task DTO to enrich.</param>
    /// <param name="fileUrlProvider">The file URL provider used to resolve avatar and attachment URLs.</param>
    /// <param name="currentUserId">The unique identifier of the current user.</param>
    /// <param name="unreadCommentsCount">The unread comment count for this task and user.</param>
    /// <returns>A copy of <paramref name="task"/> with resolved URLs and unread comment count.</returns>
    public static TaskDto Enrich(
        this TaskDto task,
        IFileUrlProvider fileUrlProvider,
        Guid currentUserId,
        int unreadCommentsCount)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(fileUrlProvider);

        return task with
        {
            UnreadCommentsCount = unreadCommentsCount,
            AssignedUsers = EnrichAssignees(task.AssignedUsers, fileUrlProvider, currentUserId),
            Attachments = task.Attachments.EnrichUrls(task.ProjectId, fileUrlProvider),
        };
    }

    /// <summary>
    /// Resolves assignee avatars and the unread comment count for a <see cref="TaskSummaryDto"/>.
    /// </summary>
    /// <param name="task">The projected task summary DTO to enrich.</param>
    /// <param name="fileUrlProvider">The file URL provider used to resolve assignee avatar URLs.</param>
    /// <param name="currentUserId">The unique identifier of the current user.</param>
    /// <param name="unreadCommentsCount">The unread comment count for this task and user.</param>
    /// <returns>A copy of <paramref name="task"/> with resolved assignee avatar URLs and unread comment count.</returns>
    public static TaskSummaryDto Enrich(
        this TaskSummaryDto task,
        IFileUrlProvider fileUrlProvider,
        Guid currentUserId,
        int unreadCommentsCount)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(fileUrlProvider);

        return task with
        {
            UnreadCommentsCount = unreadCommentsCount,
            AssignedUsers = EnrichAssignees(task.AssignedUsers, fileUrlProvider, currentUserId),
        };
    }

    /// <summary>
    /// Resolves assignee avatars and unread comment counts for a sequence of <see cref="TaskSummaryDto"/> items.
    /// </summary>
    /// <param name="tasks">The sequence of projected task summary DTOs to enrich.</param>
    /// <param name="fileUrlProvider">The file URL provider used to resolve avatar URLs.</param>
    /// <param name="currentUserId">The unique identifier of the current user.</param>
    /// <param name="unreadCounts">A dictionary mapping task IDs to their unread comment counts for the current user.</param>
    /// <returns>A list of enriched <see cref="TaskSummaryDto"/> items.</returns>
    public static List<TaskSummaryDto> Enrich(
        this IEnumerable<TaskSummaryDto> tasks,
        IFileUrlProvider fileUrlProvider,
        Guid currentUserId,
        IReadOnlyDictionary<Guid, int> unreadCounts)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(fileUrlProvider);
        ArgumentNullException.ThrowIfNull(unreadCounts);

        return
        [
            .. tasks.Select(task => task.Enrich(
                fileUrlProvider,
                currentUserId,
                unreadCounts.GetValueOrDefault(task.Id))),
        ];
    }

    private static List<TaskAssigneeDto> EnrichAssignees(
        IReadOnlyList<TaskAssigneeDto> assignees,
        IFileUrlProvider fileUrlProvider,
        Guid currentUserId)
        =>
        [
            .. assignees.Select(user => user with
            {
                AvatarUrl = fileUrlProvider.ResolveUserAvatarUrl(
                    user.AvatarUrl,
                    user.Id,
                    user.Id == currentUserId),
            }),
        ];
}
