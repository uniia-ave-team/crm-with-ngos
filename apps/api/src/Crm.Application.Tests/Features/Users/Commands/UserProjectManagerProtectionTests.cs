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
/// Unit tests verifying project management continuity during user deactivation and deletion:
/// administrators can deactivate or delete a sole project manager by taking over management of
/// those projects, while self-deletion (<c>DELETE /users/me</c>) remains blocked until transferred.
/// </summary>
public class UserProjectManagerProtectionTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IAuthRoleRepository _roleRepository = Substitute.For<IAuthRoleRepository>();
    private readonly IProjectRepository _projectRepository = Substitute.For<IProjectRepository>();
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly IFileTransactionTracker _fileTracker = Substitute.For<IFileTransactionTracker>();

    [Fact]
    public async Task DeactivateUserWhenSoleActiveManagerOfProjectShouldTransferManagementToActingAdminAndDeactivate()
    {
        var userId = Guid.NewGuid();
        var actingAdminId = Guid.NewGuid();
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

        await handler.Handle(new DeactivateUserCommand(userId, actingAdminId), CancellationToken.None);

        await _projectRepository.Received(1).EnsureManagerForProjectsAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { projectId })),
            actingAdminId,
            Arg.Any<CancellationToken>());
        await _userRepository.Received(1).SetUserActivationStatusAsync(userId, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateUserWhenNotSoleProjectManagerShouldDeactivateWithoutTransfer()
    {
        var userId = Guid.NewGuid();
        var actingAdminId = Guid.NewGuid();

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

        await handler.Handle(new DeactivateUserCommand(userId, actingAdminId), CancellationToken.None);

        await _projectRepository.DidNotReceive().EnsureManagerForProjectsAsync(
            Arg.Any<IEnumerable<Guid>>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await _userRepository.Received(1).SetUserActivationStatusAsync(userId, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteUserByAdminWhenSoleActiveManagerOfProjectShouldTransferManagementToActingAdminAndDelete()
    {
        var userId = Guid.NewGuid();
        var actingAdminId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        _roleRepository.IsUserLastAdminAsync(userId, Arg.Any<CancellationToken>())
            .Returns(false);
        _projectRepository.GetSoleManagedProjectIdsForUpdateAsync(userId, Arg.Any<CancellationToken>())
            .Returns([projectId]);
        _userRepository.GetAvatarAsync(userId, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<string>(new EntityFieldNotFoundException<User>(nameof(User.Avatar))));

        var handler = new DeleteUserCommandHandler(
            _identityService,
            _roleRepository,
            _userRepository,
            _projectRepository,
            _fileTracker,
            Substitute.For<ILogger<DeleteUserCommandHandler>>());

        await handler.Handle(new DeleteUserCommand(userId, actingAdminId), CancellationToken.None);

        await _projectRepository.Received(1).EnsureManagerForProjectsAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { projectId })),
            actingAdminId,
            Arg.Any<CancellationToken>());
        await _identityService.Received(1).DeleteUserAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteUserSelfWhenSoleActiveManagerOfProjectShouldThrowAndNotDelete()
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
            handler.Handle(new DeleteUserCommand(userId, userId), CancellationToken.None).AsTask());

        await _projectRepository.DidNotReceive().EnsureManagerForProjectsAsync(
            Arg.Any<IEnumerable<Guid>>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await _identityService.DidNotReceive().DeleteUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteUserWhenNotSoleProjectManagerShouldDeleteWithoutTransfer()
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

        await _projectRepository.DidNotReceive().EnsureManagerForProjectsAsync(
            Arg.Any<IEnumerable<Guid>>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await _identityService.Received(1).DeleteUserAsync(userId, Arg.Any<CancellationToken>());
    }
}
