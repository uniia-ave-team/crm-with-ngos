using System.Text.Json.Nodes;
using Crm.Application.Dtos.Project.Commands;
using Crm.Application.Dtos.Task;
using Crm.Application.Dtos.Task.Commands;
using Crm.Application.Features.Projects.Commands;
using Crm.Application.Features.Tasks.Commands;
using Crm.Application.Features.Tasks.Mappings;
using Crm.Application.Features.Tasks.Services;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.Tasks.Commands;

/// <summary>
/// Unit tests for task creation, update, status/end-date transitions, comments, topics, tags, and deletion handlers.
/// </summary>
public class TaskCommandHandlerTests
{
    private readonly IProjectRepository _projectRepository = Substitute.For<IProjectRepository>();
    private readonly ITopicRepository _topicRepository = Substitute.For<ITopicRepository>();
    private readonly ITagRepository _tagRepository = Substitute.For<ITagRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IProjectTaskRepository _taskRepository = Substitute.For<IProjectTaskRepository>();
    private readonly ITaskAttachmentRepository _attachmentRepository = Substitute.For<ITaskAttachmentRepository>();
    private readonly IFileTransactionTracker _fileTracker = Substitute.For<IFileTransactionTracker>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly IProjectAccessService _projectAccessService = Substitute.For<IProjectAccessService>();
    private readonly ITaskAssigneeValidator _assigneeValidator;

    private readonly DateTime _utcNow = new(2026, 11, 16, 16, 0, 0, DateTimeKind.Utc);

    static TaskCommandHandlerTests()
    {
        new TaskMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
    }

    public TaskCommandHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(new DateTimeOffset(_utcNow));
        _assigneeValidator = new TaskAssigneeValidator(_userRepository, _projectRepository);
    }

    [Fact]
    public async Task CreateTaskWhenCreatedWithCompletedStatusAndNullEndDateShouldAutoPopulateEndDateAndNormalizeLinksAndMetadata()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var startDate = new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);
        _projectAccessService.GetRequiredAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(new ProjectAccess(ProjectVisibility.Public, userId, ProjectClaim.Manage, userId));

        ProjectTask? created = null;
        _taskRepository.CreateAsync(Arg.Do<ProjectTask>(t => created = t), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<ProjectTask>());

        var handler = new CreateTaskCommandHandler(
            _topicRepository,
            _tagRepository,
            _taskRepository,
            _projectAccessService,
            _assigneeValidator,
            _timeProvider,
            Substitute.For<ILogger<CreateTaskCommandHandler>>());

        var command = new CreateTaskCommand(
            topicId,
            "  Migrated Task  ",
            [],
            [],
            "**Markdown** body",
            startDate,
            ProjectTaskStatus.Completed,
            TaskPriority.High,
            EndDate: null,
            ExternalLinks: [new TaskExternalLinkDto("  Docs  ", "  https://example.org/docs  ")],
            Metadata: new JsonObject { ["grant"] = "2026-Q4" })
        {
            ProjectId = projectId,
        };

        await handler.Handle(command, CancellationToken.None);

        await _topicRepository.Received(1).EnsureExistsInProjectAsync(projectId, topicId, Arg.Any<CancellationToken>());
        created.ShouldNotBeNull();
        created.Title.ShouldBe("Migrated Task");
        created.TopicId.ShouldBe(topicId);
        created.StartDate.ShouldBe(startDate);
        created.EndDate.ShouldBe(_utcNow);
        created.EndDateWasAuto.ShouldBeTrue();
        created.Status.ShouldBe(ProjectTaskStatus.Completed);
        created.ExternalLinks.ShouldHaveSingleItem().Title.ShouldBe("Docs");
        created.ExternalLinks.Single().Url.ShouldBe("https://example.org/docs");
        created.Metadata["grant"]?.GetValue<string>().ShouldBe("2026-Q4");
    }

    [Fact]
    public async Task UpdateTaskStatusShouldHandleFiveStatusesAutoEndDateReopenAndDateRangeValidation()
    {
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var startDate = new DateTime(2026, 11, 15, 10, 0, 0, DateTimeKind.Utc);

        var existingTask = new ProjectTask
        {
            Id = taskId,
            ProjectId = projectId,
            Title = "Task",
            StartDate = startDate,
            Status = ProjectTaskStatus.InProgress,
            EndDate = null,
        };

        _taskRepository.GetByProjectAndIdForUpdateAsync(projectId, taskId, Arg.Any<CancellationToken>())
            .Returns(existingTask);

        var handler = new UpdateTaskStatusCommandHandler(
            _topicRepository,
            _taskRepository,
            _timeProvider,
            Substitute.For<ILogger<UpdateTaskStatusCommandHandler>>());

        // Done does NOT auto-populate EndDate
        await handler.Handle(
            new UpdateTaskStatusCommand(ProjectTaskStatus.Done) { ProjectId = projectId, TaskId = taskId },
            CancellationToken.None);

        existingTask.Status.ShouldBe(ProjectTaskStatus.Done);
        existingTask.EndDate.ShouldBeNull();
        existingTask.EndDateWasAuto.ShouldBeFalse();

        // Completed with null EndDate auto-populates EndDate and sets EndDateWasAuto = true
        await handler.Handle(
            new UpdateTaskStatusCommand(ProjectTaskStatus.Completed) { ProjectId = projectId, TaskId = taskId },
            CancellationToken.None);

        existingTask.Status.ShouldBe(ProjectTaskStatus.Completed);
        existingTask.EndDate.ShouldBe(_utcNow);
        existingTask.EndDateWasAuto.ShouldBeTrue();
        existingTask.UpdatedAt.ShouldBe(_utcNow);

        // Reopen clears auto-populated EndDate
        await handler.Handle(
            new UpdateTaskStatusCommand(ProjectTaskStatus.InProgress) { ProjectId = projectId, TaskId = taskId },
            CancellationToken.None);

        existingTask.Status.ShouldBe(ProjectTaskStatus.InProgress);
        existingTask.EndDate.ShouldBeNull();
        existingTask.EndDateWasAuto.ShouldBeFalse();

        // Explicit EndDate earlier than StartDate throws InvalidTaskDateRangeException
        await Should.ThrowAsync<InvalidTaskDateRangeException>(() => handler.Handle(
            new UpdateTaskStatusCommand(ProjectTaskStatus.Completed, EndDate: startDate.AddHours(-1))
            {
                ProjectId = projectId,
                TaskId = taskId,
            },
            CancellationToken.None).AsTask());

        // Explicit valid EndDate is preserved on subsequent drag-and-drop and reopen
        var manualEndDate = startDate.AddDays(5);
        await handler.Handle(
            new UpdateTaskStatusCommand(ProjectTaskStatus.Completed, EndDate: manualEndDate)
            {
                ProjectId = projectId,
                TaskId = taskId,
            },
            CancellationToken.None);
        existingTask.EndDate.ShouldBe(manualEndDate);
        existingTask.EndDateWasAuto.ShouldBeFalse();

        await handler.Handle(
            new UpdateTaskStatusCommand(ProjectTaskStatus.InProgress) { ProjectId = projectId, TaskId = taskId },
            CancellationToken.None);
        existingTask.Status.ShouldBe(ProjectTaskStatus.InProgress);
        existingTask.EndDate.ShouldBe(manualEndDate);
    }

    [Fact]
    public async Task DeleteTaskShouldRegisterStoredAttachmentsForDeferredDeletionWithoutLoadingTaskGraph()
    {
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        _attachmentRepository.GetStoredFileNamesByTaskIdAsync(taskId, Arg.Any<CancellationToken>())
            .Returns(["task-doc.pdf"]);

        var handler = new DeleteTaskCommandHandler(
            _taskRepository,
            _attachmentRepository,
            _fileTracker,
            Substitute.For<ILogger<DeleteTaskCommandHandler>>());

        await handler.Handle(new DeleteTaskCommand(projectId, taskId), CancellationToken.None);

        _fileTracker.Received(1).RegisterFileForDeletion("task-doc.pdf", FileStorageConstants.TaskAttachmentsFolder);
        await _taskRepository.Received(1).DeleteAsync(taskId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateTaskCommentShouldPersistCommentAndMarkUnreadForOtherUsers()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var commentRepository = Substitute.For<ITaskCommentRepository>();
        _currentUserService.GetUserId().Returns(userId);

        TaskComment? createdComment = null;
        commentRepository.CreateAsync(Arg.Do<TaskComment>(c => createdComment = c), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<TaskComment>());

        var handler = new CreateTaskCommentCommandHandler(
            commentRepository,
            _currentUserService,
            _timeProvider,
            Substitute.For<ILogger<CreateTaskCommentCommandHandler>>());

        var command = new CreateTaskCommentCommand("  Looks good  ")
        {
            ProjectId = projectId,
            TaskId = taskId,
        };

        await handler.Handle(command, CancellationToken.None);

        createdComment.ShouldNotBeNull();
        createdComment.UserId.ShouldBe(userId);
        createdComment.Content.ShouldBe("Looks good");
        createdComment.CreatedAt.ShouldBe(_utcNow);
        createdComment.UpdatedAt.ShouldBe(_utcNow);
        await commentRepository.Received(1).MarkUnreadForOtherUsersAsync(createdComment, userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkTaskCommentsReadShouldDeleteReadStateForCurrentUser()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var commentRepository = Substitute.For<ITaskCommentRepository>();
        _currentUserService.GetUserId().Returns(userId);

        var handler = new MarkTaskCommentsReadCommandHandler(
            commentRepository,
            _currentUserService,
            Substitute.For<ILogger<MarkTaskCommentsReadCommandHandler>>());

        await handler.Handle(new MarkTaskCommentsReadCommand(projectId, taskId), CancellationToken.None);

        await commentRepository.Received(1).MarkAsReadAsync(taskId, userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateTaskCommentWhenViewerEditsOwnCommentShouldSucceed()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var commentRepository = Substitute.For<ITaskCommentRepository>();
        _currentUserService.GetUserId().Returns(userId);

        var comment = new TaskComment
        {
            Id = commentId,
            TaskId = taskId,
            UserId = userId,
            Content = "Initial",
        };
        commentRepository.GetByTaskAndIdForUpdateAsync(taskId, commentId, Arg.Any<CancellationToken>())
            .Returns(comment);

        var handler = new UpdateTaskCommentCommandHandler(
            commentRepository,
            _currentUserService,
            _timeProvider,
            Substitute.For<ILogger<UpdateTaskCommentCommandHandler>>());

        var command = new UpdateTaskCommentCommand("  Updated  ")
        {
            ProjectId = projectId,
            TaskId = taskId,
            CommentId = commentId,
        };

        await handler.Handle(command, CancellationToken.None);

        comment.Content.ShouldBe("Updated");
        comment.UpdatedAt.ShouldBe(_utcNow);
    }

    [Fact]
    public async Task DeleteTaskCommentWhenViewerDeletesAnotherUsersCommentShouldThrowUnauthorizedAccessException()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var commentRepository = Substitute.For<ITaskCommentRepository>();
        _currentUserService.GetUserId().Returns(userId);

        commentRepository.GetByTaskAndIdForUpdateAsync(taskId, commentId, Arg.Any<CancellationToken>())
            .Returns(new TaskComment
            {
                Id = commentId,
                TaskId = taskId,
                UserId = otherUserId,
                Content = "Someone else's comment",
            });

        _projectAccessService.GetRequiredAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(new ProjectAccess(ProjectVisibility.Private, otherUserId, ProjectClaim.Edit, userId));

        var handler = new DeleteTaskCommentCommandHandler(
            commentRepository,
            _currentUserService,
            _projectAccessService,
            Substitute.For<ILogger<DeleteTaskCommentCommandHandler>>());

        await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new DeleteTaskCommentCommand(projectId, taskId, commentId), CancellationToken.None).AsTask());

        commentRepository.GetByTaskAndIdForUpdateAsync(taskId, commentId, Arg.Any<CancellationToken>())
            .Returns(new TaskComment
            {
                Id = commentId,
                TaskId = taskId,
                UserId = userId,
                Content = "Own comment",
            });

        await handler.Handle(new DeleteTaskCommentCommand(projectId, taskId, commentId), CancellationToken.None);
        await commentRepository.Received(1).DeleteAsync(commentId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteTopicWhenTopicContainsTasksShouldThrowCannotDeleteTopicWithTasksException()
    {
        var projectId = Guid.NewGuid();
        var topicId = Guid.NewGuid();

        _topicRepository.HasTasksAsync(topicId, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new DeleteTopicCommandHandler(
            _topicRepository,
            Substitute.For<ILogger<DeleteTopicCommandHandler>>());

        await Should.ThrowAsync<CannotDeleteTopicWithTasksException>(() =>
            handler.Handle(new DeleteTopicCommand(projectId, topicId), CancellationToken.None).AsTask());

        await _topicRepository.Received(1).EnsureExistsInProjectAsync(projectId, topicId, Arg.Any<CancellationToken>());
        await _topicRepository.DidNotReceive().DeleteAsync(topicId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteTopicWhenTopicHasNoTasksShouldDeleteTopic()
    {
        var projectId = Guid.NewGuid();
        var topicId = Guid.NewGuid();

        _topicRepository.HasTasksAsync(topicId, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new DeleteTopicCommandHandler(
            _topicRepository,
            Substitute.For<ILogger<DeleteTopicCommandHandler>>());

        await handler.Handle(new DeleteTopicCommand(projectId, topicId), CancellationToken.None);

        await _topicRepository.Received(1).EnsureExistsInProjectAsync(projectId, topicId, Arg.Any<CancellationToken>());
        await _topicRepository.Received(1).DeleteAsync(topicId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteTagShouldVerifyTagBelongsToProjectBeforeDeleting()
    {
        var projectId = Guid.NewGuid();
        var tagId = Guid.NewGuid();

        var handler = new DeleteTagCommandHandler(
            _tagRepository,
            Substitute.For<ILogger<DeleteTagCommandHandler>>());

        await handler.Handle(new DeleteTagCommand(projectId, tagId), CancellationToken.None);

        await _tagRepository.Received(1).EnsureExistsInProjectAsync(projectId, tagId, Arg.Any<CancellationToken>());
        await _tagRepository.Received(1).DeleteAsync(tagId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAndRemoveTagOnTaskShouldDelegateToTagRepositoryAndExecuteUpdateTimestamp()
    {
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var laterUtcNow = _utcNow.AddMinutes(5);

        _tagRepository.AddToTaskAsync(taskId, tagId, Arg.Any<CancellationToken>()).Returns(true);
        _tagRepository.RemoveFromTaskAsync(taskId, tagId, Arg.Any<CancellationToken>()).Returns(true);

        var addHandler = new AddTagToTaskCommandHandler(
            _taskRepository,
            _tagRepository,
            _timeProvider,
            Substitute.For<ILogger<AddTagToTaskCommandHandler>>());

        await addHandler.Handle(new AddTagToTaskCommand(projectId, taskId, tagId), CancellationToken.None);

        await _tagRepository.Received(1).EnsureExistsInProjectAsync(projectId, tagId, Arg.Any<CancellationToken>());
        await _tagRepository.Received(1).AddToTaskAsync(taskId, tagId, Arg.Any<CancellationToken>());
        await _taskRepository.Received(1).UpdateUpdatedAtAsync(taskId, _utcNow, Arg.Any<CancellationToken>());

        _timeProvider.GetUtcNow().Returns(new DateTimeOffset(laterUtcNow));
        var removeHandler = new RemoveTagFromTaskCommandHandler(
            _taskRepository,
            _tagRepository,
            _timeProvider,
            Substitute.For<ILogger<RemoveTagFromTaskCommandHandler>>());

        await removeHandler.Handle(new RemoveTagFromTaskCommand(projectId, taskId, tagId), CancellationToken.None);

        await _tagRepository.Received(2).EnsureExistsInProjectAsync(projectId, tagId, Arg.Any<CancellationToken>());
        await _tagRepository.Received(1).RemoveFromTaskAsync(taskId, tagId, Arg.Any<CancellationToken>());
        await _taskRepository.Received(1).UpdateUpdatedAtAsync(taskId, laterUtcNow, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateTaskWithAssigneesAndTagsShouldValidateInApplicationLayerAndAttachIdsOnly()
    {
        var creatorId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var validMemberId = Guid.NewGuid();
        var deactivatedUserId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var tagId = Guid.NewGuid();

        _projectAccessService.GetRequiredAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(new ProjectAccess(ProjectVisibility.Private, creatorId, ProjectClaim.Manage, creatorId));
        _projectRepository.GetMemberUserIdsAsync(projectId, Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([validMemberId]);
        _userRepository.GetActiveStatusesAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, bool>
            {
                [creatorId] = true,
                [validMemberId] = true,
                [deactivatedUserId] = false,
                [outsiderId] = true,
            });

        ProjectTask? created = null;
        _taskRepository.CreateAsync(Arg.Do<ProjectTask>(t => created = t), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<ProjectTask>());

        var handler = new CreateTaskCommandHandler(
            _topicRepository,
            _tagRepository,
            _taskRepository,
            _projectAccessService,
            _assigneeValidator,
            _timeProvider,
            Substitute.For<ILogger<CreateTaskCommandHandler>>());

        await Should.ThrowAsync<CannotAssignUserToTaskException>(() => handler.Handle(
            new CreateTaskCommand(topicId, "Bad Inactive", [deactivatedUserId], [], null, _utcNow, ProjectTaskStatus.ToDo, TaskPriority.Low)
            {
                ProjectId = projectId,
            },
            CancellationToken.None).AsTask());

        await Should.ThrowAsync<CannotAssignUserToTaskException>(() => handler.Handle(
            new CreateTaskCommand(topicId, "Bad Outsider", [outsiderId], [], null, _utcNow, ProjectTaskStatus.ToDo, TaskPriority.Low)
            {
                ProjectId = projectId,
            },
            CancellationToken.None).AsTask());

        await handler.Handle(
            new CreateTaskCommand(topicId, "Valid Task", [creatorId, validMemberId, validMemberId], [tagId, tagId], null, _utcNow, ProjectTaskStatus.ToDo, TaskPriority.Medium)
            {
                ProjectId = projectId,
            },
            CancellationToken.None);

        await _tagRepository.Received(1).EnsureAllExistInProjectAsync(projectId, Arg.Is<IEnumerable<Guid>>(ids => ids.Distinct().SequenceEqual(new[] { tagId })), Arg.Any<CancellationToken>());
        created.ShouldNotBeNull();
        await _taskRepository.Received(1).AssignToTaskAsync(
            created.Id,
            Arg.Is<IEnumerable<Guid>>(ids => ids.Distinct().SequenceEqual(new[] { creatorId, validMemberId })),
            Arg.Any<CancellationToken>());
        await _tagRepository.Received(1).AddToTaskAsync(
            created.Id,
            Arg.Is<IEnumerable<Guid>>(ids => ids.Distinct().SequenceEqual(new[] { tagId })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateTaskAndAssignUnassignShouldMutateRelationsAndTimestampInApplicationLayer()
    {
        var userId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var tagId = Guid.NewGuid();

        _projectAccessService.GetRequiredAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(new ProjectAccess(ProjectVisibility.Public, userId, ProjectClaim.Manage, userId));
        _userRepository.GetActiveStatusesAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, bool>
            {
                [userId] = true,
                [secondUserId] = true,
            });

        var existingTask = new ProjectTask
        {
            Id = taskId,
            ProjectId = projectId,
            TopicId = topicId,
            Title = "Initial",
            StartDate = _utcNow.AddDays(-1),
        };

        _taskRepository.GetByProjectAndIdForUpdateAsync(projectId, taskId, Arg.Any<CancellationToken>())
            .Returns(existingTask);
        _taskRepository.AssignToTaskAsync(taskId, userId, Arg.Any<CancellationToken>()).Returns(true);
        _taskRepository.UnassignFromTaskAsync(taskId, secondUserId, Arg.Any<CancellationToken>()).Returns(true);

        var updateHandler = new UpdateTaskCommandHandler(
            _topicRepository,
            _tagRepository,
            _taskRepository,
            _projectAccessService,
            _assigneeValidator,
            _timeProvider,
            Substitute.For<ILogger<UpdateTaskCommandHandler>>());

        await updateHandler.Handle(
            new UpdateTaskCommand(
                topicId,
                "Updated",
                [secondUserId],
                [tagId],
                null,
                _utcNow,
                ProjectTaskStatus.Completed,
                TaskPriority.High,
                ExternalLinks: [new TaskExternalLinkDto("Site", "https://ngo.org")],
                Metadata: new JsonObject { ["phase"] = 2 })
            {
                ProjectId = projectId,
                TaskId = taskId,
            },
            CancellationToken.None);

        existingTask.Title.ShouldBe("Updated");
        existingTask.Status.ShouldBe(ProjectTaskStatus.Completed);
        existingTask.EndDate.ShouldBe(_utcNow);
        existingTask.EndDateWasAuto.ShouldBeTrue();
        existingTask.UpdatedAt.ShouldBe(_utcNow);
        existingTask.ExternalLinks.ShouldHaveSingleItem().Url.ShouldBe("https://ngo.org");
        existingTask.Metadata["phase"]?.GetValue<int>().ShouldBe(2);

        await _taskRepository.Received(1).SyncTaskUsersAsync(
            taskId,
            Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { secondUserId })),
            Arg.Any<CancellationToken>());
        await _tagRepository.Received(1).SyncTaskTagsAsync(
            taskId,
            Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { tagId })),
            Arg.Any<CancellationToken>());

        var laterUtc = _utcNow.AddMinutes(10);
        _timeProvider.GetUtcNow().Returns(new DateTimeOffset(laterUtc));

        var assignHandler = new AssignUserToTaskCommandHandler(
            _taskRepository,
            _projectAccessService,
            _assigneeValidator,
            _timeProvider,
            Substitute.For<ILogger<AssignUserToTaskCommandHandler>>());

        await assignHandler.Handle(new AssignUserToTaskCommand(projectId, taskId, userId), CancellationToken.None);
        await _taskRepository.Received(1).AssignToTaskAsync(taskId, userId, Arg.Any<CancellationToken>());
        await _taskRepository.Received(1).UpdateUpdatedAtAsync(taskId, laterUtc, Arg.Any<CancellationToken>());

        var unassignHandler = new UnassignUserFromTaskCommandHandler(
            _taskRepository,
            _timeProvider,
            Substitute.For<ILogger<UnassignUserFromTaskCommandHandler>>());

        await unassignHandler.Handle(new UnassignUserFromTaskCommand(projectId, taskId, secondUserId), CancellationToken.None);
        await _taskRepository.Received(1).UnassignFromTaskAsync(taskId, secondUserId, Arg.Any<CancellationToken>());
        await _taskRepository.Received(2).UpdateUpdatedAtAsync(taskId, laterUtc, Arg.Any<CancellationToken>());
    }
}
