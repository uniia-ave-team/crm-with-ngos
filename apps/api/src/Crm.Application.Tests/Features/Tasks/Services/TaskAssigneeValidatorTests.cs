using Crm.Application.Features.Tasks.Services;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.Tasks.Services;

/// <summary>
/// Unit tests for <see cref="TaskAssigneeValidator"/> verifying user existence, active status,
/// private project membership, and deduplication.
/// </summary>
public class TaskAssigneeValidatorTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IProjectRepository _projectRepository = Substitute.For<IProjectRepository>();

    [Fact]
    public async Task ValidateAsyncShouldSkipRepositoryCallsWhenAssigneeListIsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var validator = new TaskAssigneeValidator(_userRepository, _projectRepository);
        var access = new ProjectAccess(ProjectVisibility.Private, ProjectClaim.Manage, Guid.NewGuid());

        await validator.ValidateAsync([], Guid.NewGuid(), access, ct);

        await _userRepository.DidNotReceiveWithAnyArgs().GetActiveStatusesAsync(default, ct);
        await _projectRepository.DidNotReceiveWithAnyArgs().GetMemberUserIdsAsync(default, default, ct);
    }

    [Fact]
    public async Task ValidateAsyncShouldThrowWhenUserMissingDeactivatedOrOutsiderInPrivateProject()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var missingId = Guid.NewGuid();
        var inactiveId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();

        var access = new ProjectAccess(ProjectVisibility.Private, ProjectClaim.Manage, managerId);
        var validator = new TaskAssigneeValidator(_userRepository, _projectRepository);

        _userRepository.GetActiveStatusesAsync(Arg.Any<IEnumerable<Guid>>(), ct)
            .Returns(new Dictionary<Guid, bool>
            {
                [managerId] = true,
                [memberId] = true,
                [inactiveId] = false,
                [outsiderId] = true,
            });
        _projectRepository.GetMemberUserIdsAsync(projectId, Arg.Any<IEnumerable<Guid>>(), ct)
            .Returns([managerId, memberId]);

        await validator.ValidateAsync([managerId, memberId, memberId], projectId, access, ct);

        await Should.ThrowAsync<EntityNotFoundException<User>>(() =>
            validator.ValidateAsync([missingId], projectId, access, ct));

        await Should.ThrowAsync<CannotAssignUserToTaskException>(() =>
            validator.ValidateAsync([inactiveId], projectId, access, ct));

        await Should.ThrowAsync<CannotAssignUserToTaskException>(() =>
            validator.ValidateAsync([outsiderId], projectId, access, ct));
    }
}
