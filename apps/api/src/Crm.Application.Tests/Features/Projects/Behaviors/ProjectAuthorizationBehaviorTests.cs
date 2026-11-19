using Crm.Application.Common.Behaviors;
using Crm.Application.Common.Security;
using Crm.Application.Dtos.Project;
using Crm.Application.Dtos.Project.Commands;
using Crm.Application.Dtos.Project.Queries;
using Crm.Application.Dtos.Task;
using Crm.Application.Dtos.Task.Commands;
using Crm.Application.Dtos.Task.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.Projects.Behaviors;

/// <summary>
/// Unit tests for <see cref="ProjectAuthorizationBehavior{TRequest, TResponse}"/> verifying centralized
/// project-level authorization, 404 vs 403 semantics, task scoping, and global cross-project queries.
/// </summary>
public class ProjectAuthorizationBehaviorTests
{
    private readonly IProjectRepository _projectRepository = Substitute.For<IProjectRepository>();
    private readonly IProjectTaskRepository _taskRepository = Substitute.For<IProjectTaskRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ProjectAccessService _accessService;

    public ProjectAuthorizationBehaviorTests()
    {
        _accessService = new ProjectAccessService(_projectRepository, _currentUserService);
    }

    [Fact]
    public async Task PrivateProjectWithoutViewAccessShouldThrowEntityNotFoundException()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _currentUserService.GetUserId().Returns(userId);
        _projectRepository.GetAccessAsync(projectId, userId, Arg.Any<CancellationToken>())
            .Returns(new ProjectAccess(ProjectVisibility.Private, Guid.NewGuid(), Claim: null, userId));

        var behavior = new ProjectAuthorizationBehavior<GetProjectByIdQuery, ProjectDto>(
            _accessService,
            _taskRepository);

        await Should.ThrowAsync<EntityNotFoundException<Project>>(() =>
            behavior.Handle(
                new GetProjectByIdQuery(projectId),
                (_, _) => ValueTask.FromResult<ProjectDto>(default),
                CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task PublicProjectNonMemberShouldFailManageCheckWithUnauthorizedAccessException()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _currentUserService.GetUserId().Returns(userId);
        _projectRepository.GetAccessAsync(projectId, userId, Arg.Any<CancellationToken>())
            .Returns(new ProjectAccess(ProjectVisibility.Public, Guid.NewGuid(), Claim: null, userId));

        var behavior = new ProjectAuthorizationBehavior<DeleteProjectCommand, Unit>(
            _accessService,
            _taskRepository);

        await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            behavior.Handle(
                new DeleteProjectCommand(projectId),
                (_, _) => ValueTask.FromResult(Unit.Value),
                CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task TaskScopedRequestShouldVerifyTaskMembershipAndCacheAccessWithinScope()
    {
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var expectedAccess = new ProjectAccess(ProjectVisibility.Public, userId, ProjectClaim.Manage, userId);

        _currentUserService.GetUserId().Returns(userId);
        _projectRepository.GetAccessAsync(projectId, userId, Arg.Any<CancellationToken>())
            .Returns(expectedAccess);

        var behavior = new ProjectAuthorizationBehavior<DeleteTaskCommand, Unit>(
            _accessService,
            _taskRepository);

        ProjectAccess? observedInsideHandler = null;
        await behavior.Handle(
            new DeleteTaskCommand(projectId, taskId),
            async (_, ct) =>
            {
                observedInsideHandler = await _accessService.GetRequiredAsync(projectId, ct);
                return Unit.Value;
            },
            CancellationToken.None);

        await _taskRepository.Received(1).EnsureExistsInProjectAsync(projectId, taskId, Arg.Any<CancellationToken>());
        await _projectRepository.Received(1).GetAccessAsync(projectId, userId, Arg.Any<CancellationToken>());
        observedInsideHandler.ShouldBe(expectedAccess);
    }

    [Fact]
    public async Task GlobalTaskQueryWithNullProjectIdShouldBypassPerProjectLookup()
    {
        var behavior = new ProjectAuthorizationBehavior<GetProjectTasksQuery, PagedResult<TaskSummaryDto>>(
            _accessService,
            _taskRepository);

        bool nextCalled = false;
        await behavior.Handle(
            new GetProjectTasksQuery(Filter: "mine") { ProjectId = null },
            (_, _) =>
            {
                nextCalled = true;
                return ValueTask.FromResult(new PagedResult<TaskSummaryDto>([], 0, 1, 10));
            },
            CancellationToken.None);

        nextCalled.ShouldBeTrue();
        await _projectRepository.DidNotReceive().GetAccessAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
