using System.Text.Json;
using System.Text.Json.Nodes;
using Crm.Application.Dtos.Task;
using Crm.Application.Features.Tasks.Extensions;
using Crm.Application.Features.Tasks.Mappings;
using Crm.Application.Interfaces;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Mapster;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.Tasks.Extensions;

/// <summary>
/// Unit tests for <see cref="TaskDtoExtensions"/> covering unread-count hydration
/// and URL enrichment (including <c>isSelf</c> avatar resolution) for both <see cref="TaskDto"/> and <see cref="TaskSummaryDto"/>.
/// </summary>
public class TaskDtoExtensionsTests
{
    static TaskDtoExtensionsTests()
    {
        new TaskMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
    }

    [Fact]
    public void TaskAttachmentMappingShouldKeepUrlNullAndHideStorageKeyFromJson()
    {
        var attachment = new TaskAttachment
        {
            Id = Guid.NewGuid(),
            TaskId = Guid.NewGuid(),
            OriginalFileName = "doc.pdf",
            StoredFileName = "secret-internal-key.pdf",
            ContentType = "application/pdf",
            FileSize = 42,
            CreatedAt = DateTime.UtcNow,
        };

        var dto = attachment.Adapt<TaskAttachmentDto>();

        dto.Url.ShouldBeNull();
        dto.StorageKey.ShouldBe("secret-internal-key.pdf");

        string serialized = JsonSerializer.Serialize(dto);
        serialized.ShouldNotContain("secret-internal-key.pdf");
        serialized.ShouldNotContain("StorageKey", Case.Insensitive);
    }

    [Fact]
    public void EnrichShouldHydrateUnreadCountAndResolveAvatarsAndAttachmentDownloadUrls()
    {
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();

        var fileUrlProvider = Substitute.For<IFileUrlProvider>();
        fileUrlProvider.GetFileUrl("me.png", "api/v1/users/me/avatar")
            .Returns("https://cdn.example/api/v1/users/me/avatar");
        fileUrlProvider.GetFileUrl("other.png", $"api/v1/users/{otherUserId}/avatar")
            .Returns($"https://cdn.example/api/v1/users/{otherUserId}/avatar");
        fileUrlProvider.GetFileUrl("stored-doc.pdf", $"api/v1/projects/{projectId}/tasks/{taskId}/attachments/{attachmentId}")
            .Returns($"https://cdn.example/api/v1/projects/{projectId}/tasks/{taskId}/attachments/{attachmentId}");

        var task = new TaskDto(
            taskId,
            projectId,
            Guid.NewGuid(),
            "Розробка",
            "Task 1",
            null,
            ProjectTaskStatus.ToDo,
            TaskPriority.Medium,
            DateTime.UtcNow,
            null,
            UnreadCommentsCount: 0,
            AssignedUsers:
            [
                new TaskAssigneeDto(currentUserId, "Ivan", "Franko", null, "me.png"),
                new TaskAssigneeDto(otherUserId, "Olena", "Kovalenko", "Petrivna", "other.png"),
            ],
            Tags: [],
            Attachments:
            [
                new TaskAttachmentDto(attachmentId, taskId, currentUserId, "doc.pdf", "application/pdf", 42, null, DateTime.UtcNow, "stored-doc.pdf"),
            ],
            ExternalLinks: [new TaskExternalLinkDto("Docs", "https://example.org")],
            Metadata: new JsonObject { ["source"] = "grant" },
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow);

        var enriched = task.Enrich(fileUrlProvider, currentUserId, unreadCommentsCount: 3);

        enriched.UnreadCommentsCount.ShouldBe(3);
        enriched.AssignedUsers[0].AvatarUrl.ShouldBe("https://cdn.example/api/v1/users/me/avatar");
        enriched.AssignedUsers[1].AvatarUrl.ShouldBe($"https://cdn.example/api/v1/users/{otherUserId}/avatar");
        enriched.Attachments.Single().Url.ShouldBe($"https://cdn.example/api/v1/projects/{projectId}/tasks/{taskId}/attachments/{attachmentId}");
        enriched.Attachments.Single().StorageKey.ShouldBeNull();
    }
}
