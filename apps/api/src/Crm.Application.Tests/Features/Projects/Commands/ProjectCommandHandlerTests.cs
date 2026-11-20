using Crm.Application.Dtos.Project.Commands;
using Crm.Application.Features.Projects.Commands;
using Crm.Application.Features.Projects.Mappings;
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

namespace Crm.Application.Tests.Features.Projects.Commands;

/// <summary>
/// Unit tests for project and project-member command handlers.
/// </summary>
public class ProjectCommandHandlerTests
{
    private readonly IProjectRepository _projectRepository = Substitute.For<IProjectRepository>();
    private readonly IProjectTaskRepository _taskRepository = Substitute.For<IProjectTaskRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ITaskAttachmentRepository _attachmentRepository = Substitute.For<ITaskAttachmentRepository>();
    private readonly IFileTransactionTracker _fileTracker = Substitute.For<IFileTransactionTracker>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IProjectAccessService _projectAccessService = Substitute.For<IProjectAccessService>();

    static ProjectCommandHandlerTests()
    {
        new ProjectMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
    }

    [Fact]
    public async Task CreateProjectShouldCreateProjectWithCreatorMemberAndDefaultClaims()
    {
        var creatorId = Guid.NewGuid();
        _currentUserService.GetUserId().Returns(creatorId);

        Project? captured = null;
        _projectRepository.CreateAsync(Arg.Do<Project>(p => captured = p), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Project>());

        var handler = new CreateProjectCommandHandler(
            _projectRepository,
            _currentUserService,
            Substitute.For<ILogger<CreateProjectCommandHandler>>());

        var startDate = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);
        var command = new CreateProjectCommand(
            "  NGO Board  ",
            "  Board description  ",
            ProjectVisibility.Private,
            startDate,
            new DateTime(2026, 12, 31, 18, 0, 0, DateTimeKind.Utc));

        await handler.Handle(command, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured.Title.ShouldBe("NGO Board");
        captured.Description.ShouldBe("Board description");
        captured.Visibility.ShouldBe(ProjectVisibility.Private);
        captured.StartDate.ShouldBe(startDate);
        captured.EndDate.ShouldBe(new DateTime(2026, 12, 31, 18, 0, 0, DateTimeKind.Utc));
        captured.CreatedBy.ShouldBe(creatorId);
        captured.Members.Count.ShouldBe(1);
        var creatorMember = captured.Members.Single();
        creatorMember.UserId.ShouldBe(creatorId);
        creatorMember.Claim.ShouldBe(ProjectClaim.Manage);
    }

    [Fact]
    public async Task UpdateProjectWhenChangingVisibilityWithoutManageClaimShouldThrowUnauthorizedAndCleanupWhenSwitchingToPrivate()
    {
        var editorId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _currentUserService.GetUserId().Returns(editorId);

        var project = new Project
        {
            Id = projectId,
            Title = "Public Project",
            Visibility = ProjectVisibility.Public,
            CreatedBy = ownerId,
        };

        _projectRepository.GetForUpdateAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(project);
        _projectAccessService.GetRequiredAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(new ProjectAccess(ProjectVisibility.Public, ProjectClaim.Edit, editorId));

        var handler = new UpdateProjectCommandHandler(
            _projectRepository,
            _taskRepository,
            _projectAccessService,
            _currentUserService,
            Substitute.For<ILogger<UpdateProjectCommandHandler>>());

        var projectStartDate = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);
        var changeVisibilityCommand = new UpdateProjectCommand(
            "Renamed",
            null,
            ProjectVisibility.Private,
            projectStartDate)
        {
            Id = projectId,
        };

        // Public project editor does NOT have Manage permission -> 403
        await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
            await handler.Handle(changeVisibilityCommand, CancellationToken.None));

        var projectEndDate = new DateTime(2027, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var renameOnlyCommand = new UpdateProjectCommand(
            "Renamed",
            "New desc",
            ProjectVisibility.Public,
            projectStartDate,
            projectEndDate)
        {
            Id = projectId,
        };

        await handler.Handle(renameOnlyCommand, CancellationToken.None);
        project.Title.ShouldBe("Renamed");
        project.Description.ShouldBe("New desc");
        project.StartDate.ShouldBe(projectStartDate);
        project.EndDate.ShouldBe(projectEndDate);
        await _taskRepository.DidNotReceive().RemoveInaccessibleUserDataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        // Owner changing visibility from Public -> Private triggers inaccessible user data cleanup (B-13)
        _projectAccessService.GetRequiredAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(new ProjectAccess(ProjectVisibility.Public, ProjectClaim.Manage, ownerId));
        _currentUserService.GetUserId().Returns(ownerId);

        await handler.Handle(changeVisibilityCommand, CancellationToken.None);
        project.Visibility.ShouldBe(ProjectVisibility.Private);
        await _taskRepository.Received(1).RemoveInaccessibleUserDataAsync(projectId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignProjectMemberShouldRejectInactiveUserAndNormalizeActiveMember()
    {
        var activeUserId = Guid.NewGuid();
        var inactiveUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        _userRepository.IsActiveAsync(inactiveUserId, Arg.Any<CancellationToken>()).Returns(false);
        _userRepository.IsActiveAsync(activeUserId, Arg.Any<CancellationToken>()).Returns(true);
        _projectRepository.MemberExistsAsync(projectId, activeUserId, Arg.Any<CancellationToken>()).Returns(false);

        ProjectMember? addedMember = null;
        _projectRepository.AddMemberAsync(Arg.Do<ProjectMember>(m => addedMember = m), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new AssignProjectMemberCommandHandler(
            _projectRepository,
            _userRepository,
            Substitute.For<ILogger<AssignProjectMemberCommandHandler>>());

        // Inactive user is rejected with DomainValidationException (B-18)
        await Should.ThrowAsync<CannotAssignUserToProjectException>(() => handler.Handle(
            new AssignProjectMemberCommand(inactiveUserId, "Member", ProjectClaim.View) { ProjectId = projectId },
            CancellationToken.None).AsTask());

        var command = new AssignProjectMemberCommand(
            activeUserId,
            "  SMM-менеджер  ",
            ProjectClaim.Edit)
        {
            ProjectId = projectId,
        };

        await handler.Handle(command, CancellationToken.None);

        await _userRepository.Received(1).IsActiveAsync(activeUserId, Arg.Any<CancellationToken>());
        addedMember.ShouldNotBeNull();
        addedMember.RoleTitle.ShouldBe("SMM-менеджер");
        addedMember.Claim.ShouldBe(ProjectClaim.Edit);
    }

    [Fact]
    public async Task RemoveAndDemoteProjectMemberShouldProtectLastManagerAndCleanupPrivateProjectAssignments()
    {
        var projectId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();

        _projectAccessService.GetRequiredAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(new ProjectAccess(ProjectVisibility.Private, ProjectClaim.Manage, managerId));

        _projectRepository.GetMemberClaimAsync(projectId, managerId, Arg.Any<CancellationToken>())
            .Returns(ProjectClaim.Manage);
        _projectRepository.HasAnotherManagerAsync(projectId, managerId, Arg.Any<CancellationToken>())
            .Returns(false);

        var removeHandler = new RemoveProjectMemberCommandHandler(
            _projectRepository,
            _taskRepository,
            _projectAccessService,
            Substitute.For<ILogger<RemoveProjectMemberCommandHandler>>());

        await Should.ThrowAsync<CannotRemoveLastProjectManagerException>(() =>
            removeHandler.Handle(new RemoveProjectMemberCommand(projectId, managerId), CancellationToken.None).AsTask());

        await _projectRepository.Received(1).LockForUpdateAsync(projectId, Arg.Any<CancellationToken>());

        _projectRepository.GetMemberForUpdateAsync(projectId, managerId, Arg.Any<CancellationToken>())
            .Returns(new ProjectMember { ProjectId = projectId, UserId = managerId, Claim = ProjectClaim.Manage });

        var updateHandler = new UpdateProjectMemberCommandHandler(
            _projectRepository,
            Substitute.For<ILogger<UpdateProjectMemberCommandHandler>>());

        await Should.ThrowAsync<CannotRemoveLastProjectManagerException>(() =>
            updateHandler.Handle(
                new UpdateProjectMemberCommand("Editor", ProjectClaim.Edit) { ProjectId = projectId, UserId = managerId },
                CancellationToken.None).AsTask());

        await _projectRepository.Received(2).LockForUpdateAsync(projectId, Arg.Any<CancellationToken>());

        // Removing a non-manager viewer succeeds and cleans up task assignments/read states on private project
        _projectRepository.GetMemberClaimAsync(projectId, viewerId, Arg.Any<CancellationToken>())
            .Returns(ProjectClaim.View);

        await removeHandler.Handle(new RemoveProjectMemberCommand(projectId, viewerId), CancellationToken.None);

        await _projectRepository.Received(1).RemoveMemberAsync(projectId, viewerId, Arg.Any<CancellationToken>());
        await _taskRepository.Received(1).RemoveInaccessibleUserDataAsync(projectId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteProjectShouldRegisterAttachmentsForDeferredDeletion()
    {
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _currentUserService.GetUserId().Returns(ownerId);

        _attachmentRepository.GetStoredFileNamesByProjectIdAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(["file1.pdf"]);

        var handler = new DeleteProjectCommandHandler(
            _projectRepository,
            _attachmentRepository,
            _fileTracker,
            _currentUserService,
            Substitute.For<ILogger<DeleteProjectCommandHandler>>());

        await handler.Handle(new DeleteProjectCommand(projectId), CancellationToken.None);

        _fileTracker.Received(1).RegisterFileForDeletion("file1.pdf", FileStorageConstants.TaskAttachmentsFolder);
        await _projectRepository.Received(1).DeleteAsync(projectId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAndUpdateTopicWhenTitleExistsShouldThrowEntityAlreadyExistsException()
    {
        var projectId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var topicRepository = Substitute.For<ITopicRepository>();

        topicRepository.ExistsWithTitleAsync(projectId, "Board", null, Arg.Any<CancellationToken>())
            .Returns(true);
        topicRepository.GetByProjectAndIdForUpdateAsync(projectId, topicId, Arg.Any<CancellationToken>())
            .Returns(new Topic { Id = topicId, ProjectId = projectId, Title = "Old" });
        topicRepository.ExistsWithTitleAsync(projectId, "Board", topicId, Arg.Any<CancellationToken>())
            .Returns(true);

        var createHandler = new CreateTopicCommandHandler(
            topicRepository,
            Substitute.For<ILogger<CreateTopicCommandHandler>>());

        await Should.ThrowAsync<EntityAlreadyExistsException>(() =>
            createHandler.Handle(new CreateTopicCommand("  Board  ", 0) { ProjectId = projectId }, CancellationToken.None).AsTask());

        var updateHandler = new UpdateTopicCommandHandler(
            topicRepository,
            Substitute.For<ILogger<UpdateTopicCommandHandler>>());

        await Should.ThrowAsync<EntityAlreadyExistsException>(() =>
            updateHandler.Handle(new UpdateTopicCommand("  Board  ", 0) { ProjectId = projectId, TopicId = topicId }, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task CreateAndUpdateTopicWhenOrderExistsShouldThrowEntityAlreadyExistsException()
    {
        var projectId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var topicRepository = Substitute.For<ITopicRepository>();

        topicRepository.ExistsWithOrderAsync(projectId, 0, null, Arg.Any<CancellationToken>())
            .Returns(true);
        topicRepository.GetByProjectAndIdForUpdateAsync(projectId, topicId, Arg.Any<CancellationToken>())
            .Returns(new Topic { Id = topicId, ProjectId = projectId, Title = "Old", Order = 1 });
        topicRepository.ExistsWithOrderAsync(projectId, 0, topicId, Arg.Any<CancellationToken>())
            .Returns(true);

        var createHandler = new CreateTopicCommandHandler(
            topicRepository,
            Substitute.For<ILogger<CreateTopicCommandHandler>>());

        await Should.ThrowAsync<EntityAlreadyExistsException>(() =>
            createHandler.Handle(new CreateTopicCommand("NewTopic", 0) { ProjectId = projectId }, CancellationToken.None).AsTask());

        var updateHandler = new UpdateTopicCommandHandler(
            topicRepository,
            Substitute.For<ILogger<UpdateTopicCommandHandler>>());

        await Should.ThrowAsync<EntityAlreadyExistsException>(() =>
            updateHandler.Handle(new UpdateTopicCommand("NewTopic", 0) { ProjectId = projectId, TopicId = topicId }, CancellationToken.None).AsTask());
    }
}
