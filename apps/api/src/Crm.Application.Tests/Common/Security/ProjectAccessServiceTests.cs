using Crm.Application.Common.Security;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Common.Security;

/// <summary>
/// Unit tests for <see cref="ProjectAccessService"/> verifying scoped caching and missing-project handling.
/// </summary>
public class ProjectAccessServiceTests
{
    [Fact]
    public async Task GetRequiredAsyncShouldCacheAccessWithinScopeAndThrowWhenProjectMissing()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = Guid.NewGuid();
        var missingProjectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var projectRepository = Substitute.For<IProjectRepository>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.GetUserId().Returns(userId);

        var expected = new ProjectAccess(ProjectVisibility.Private, userId, ProjectClaim.Manage, userId);
        projectRepository.GetAccessAsync(projectId, userId, ct).Returns(expected);
        projectRepository.GetAccessAsync(missingProjectId, userId, ct).Returns((ProjectAccess?)null);

        var service = new ProjectAccessService(projectRepository, currentUserService);

        var first = await service.GetRequiredAsync(projectId, ct);
        var second = await service.GetRequiredAsync(projectId, ct);

        first.ShouldBe(expected);
        second.ShouldBe(expected);
        await projectRepository.Received(1).GetAccessAsync(projectId, userId, ct);

        await Should.ThrowAsync<EntityNotFoundException<Project>>(() =>
            service.GetRequiredAsync(missingProjectId, ct));
    }
}
