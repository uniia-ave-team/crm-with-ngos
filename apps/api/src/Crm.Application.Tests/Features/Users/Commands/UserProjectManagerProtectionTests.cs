using Crm.Application.Dtos.User.Commands;
using Crm.Application.Features.Users.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.Users.Commands;

/// <summary>
/// Unit tests verifying that deactivating or deleting the sole active manager of any project is blocked,
/// while users who are not sole active managers can be deactivated or deleted normally.
/// </summary>
public class UserProjectManagerProtectionTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IAuthRoleRepository _roleRepository = Substitute.For<IAuthRoleRepository>();
    private readonly IProjectRepository _projectRepository = Substitute.For<IProjectRepository>();
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly IFileTransactionTracker _fileTracker = Substitute.For<IFileTransactionTracker>();

    [Fact]
    public async Task DeactivateUserWhenSoleActiveManagerOfProjectShouldThrowAndNotDeactivate()
    {
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        _userRepository.GetAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = userId, IsActive = true });
        _roleRepository.IsUserLastAdminAsync(userId, Arg.Any<CancellationToken>())
            .Returns(false);
        _projectRepository.GetSoleManagedProjectIdsForUpdateAsync(userId, Arg.Any<CancellationToken>())
            .Returns([projectId]);

        var handler = new DeactivateUserCommandHandler(
            _userRepository,
            _roleRepository,
            _projectRepository,
            Substitute.For<ILogger<DeactivateUserCommandHandler>>());

        await Should.ThrowAsync<CannotRemoveLastProjectManagerException>(() =>
            handler.Handle(new DeactivateUserCommand(userId, currentUserId), CancellationToken.None).AsTask());

        await _userRepository.DidNotReceive().SetUserActivationStatusAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateUserWhenNotSoleProjectManagerShouldSucceed()
    {
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        _userRepository.GetAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = userId, IsActive = true });
        _roleRepository.IsUserLastAdminAsync(userId, Arg.Any<CancellationToken>())
            .Returns(false);
        _projectRepository.GetSoleManagedProjectIdsForUpdateAsync(userId, Arg.Any<CancellationToken>())
            .Returns([]);

        var handler = new DeactivateUserCommandHandler(
            _userRepository,
            _roleRepository,
            _projectRepository,
            Substitute.For<ILogger<DeactivateUserCommandHandler>>());

        await handler.Handle(new DeactivateUserCommand(userId, currentUserId), CancellationToken.None);

        await _userRepository.Received(1).SetUserActivationStatusAsync(userId, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteUserWhenSoleActiveManagerOfProjectShouldThrowAndNotDelete()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        _roleRepository.IsUserLastAdminAsync(userId, Arg.Any<CancellationToken>())
            .Returns(false);
        _projectRepository.GetSoleManagedProjectIdsForUpdateAsync(userId, Arg.Any<CancellationToken>())
            .Returns([projectId]);

        var handler = new DeleteUserCommandHandler(
            _identityService,
            _roleRepository,
            _userRepository,
            _projectRepository,
            _fileTracker,
            Substitute.For<ILogger<DeleteUserCommandHandler>>());

        await Should.ThrowAsync<CannotRemoveLastProjectManagerException>(() =>
            handler.Handle(new DeleteUserCommand(userId), CancellationToken.None).AsTask());

        await _identityService.DidNotReceive().DeleteUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteUserWhenNotSoleProjectManagerShouldSucceed()
    {
        var userId = Guid.NewGuid();

        _roleRepository.IsUserLastAdminAsync(userId, Arg.Any<CancellationToken>())
            .Returns(false);
        _projectRepository.GetSoleManagedProjectIdsForUpdateAsync(userId, Arg.Any<CancellationToken>())
            .Returns([]);
        _userRepository.GetAvatarAsync(userId, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<string>(new EntityFieldNotFoundException<User>(nameof(User.Avatar))));

        var handler = new DeleteUserCommandHandler(
            _identityService,
            _roleRepository,
            _userRepository,
            _projectRepository,
            _fileTracker,
            Substitute.For<ILogger<DeleteUserCommandHandler>>());

        await handler.Handle(new DeleteUserCommand(userId), CancellationToken.None);

        await _identityService.Received(1).DeleteUserAsync(userId, Arg.Any<CancellationToken>());
    }
}
