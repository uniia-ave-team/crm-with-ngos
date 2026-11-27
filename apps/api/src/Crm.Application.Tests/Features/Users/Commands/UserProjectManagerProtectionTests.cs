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
    private readonly IUserRefreshTokenRepository _refreshTokenRepository = Substitute.For<IUserRefreshTokenRepository>();
    private readonly IRevokedUsersCache _revokedUsersCache = Substitute.For<IRevokedUsersCache>();
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly IFileTransactionTracker _fileTracker = Substitute.For<IFileTransactionTracker>();

    [Fact]
    public async Task DeactivateUserWhenSoleActiveManagerOfProjectShouldTransferManagementToActingAdminAndDeactivate()
    {
        var userId = Guid.NewGuid();
        var actingAdminId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        _userRepository.IsActiveAsync(userId, Arg.Any<CancellationToken>())
            .Returns(true);
        _roleRepository.IsUserLastAdminAsync(userId, Arg.Any<CancellationToken>())
            .Returns(false);
        _projectRepository.GetSoleManagedProjectIdsForUpdateAsync(userId, Arg.Any<CancellationToken>())
            .Returns([projectId]);

        var handler = new DeactivateUserCommandHandler(
            _userRepository,
            _roleRepository,
            _projectRepository,
            _refreshTokenRepository,
            _revokedUsersCache,
            Substitute.For<ILogger<DeactivateUserCommandHandler>>());

        await handler.Handle(new DeactivateUserCommand(userId) { CurrentUserId = actingAdminId }, CancellationToken.None);

        await _projectRepository.Received(1).EnsureManagerForProjectsAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { projectId })),
            actingAdminId,
            Arg.Any<CancellationToken>());
        await _userRepository.Received(1).SetUserActivationStatusAsync(userId, false, Arg.Any<CancellationToken>());
        await _refreshTokenRepository.Received(1).RevokeAllUserTokensAsync(userId, Arg.Any<CancellationToken>());
        await _revokedUsersCache.Received(1).RevokeUserAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateUserWhenNotSoleProjectManagerShouldDeactivateWithoutTransfer()
    {
        var userId = Guid.NewGuid();
        var actingAdminId = Guid.NewGuid();

        _userRepository.IsActiveAsync(userId, Arg.Any<CancellationToken>())
            .Returns(true);
        _roleRepository.IsUserLastAdminAsync(userId, Arg.Any<CancellationToken>())
            .Returns(false);
        _projectRepository.GetSoleManagedProjectIdsForUpdateAsync(userId, Arg.Any<CancellationToken>())
            .Returns([]);

        var handler = new DeactivateUserCommandHandler(
            _userRepository,
            _roleRepository,
            _projectRepository,
            _refreshTokenRepository,
            _revokedUsersCache,
            Substitute.For<ILogger<DeactivateUserCommandHandler>>());

        await handler.Handle(new DeactivateUserCommand(userId) { CurrentUserId = actingAdminId }, CancellationToken.None);

        await _projectRepository.DidNotReceive().EnsureManagerForProjectsAsync(
            Arg.Any<IEnumerable<Guid>>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await _userRepository.Received(1).SetUserActivationStatusAsync(userId, false, Arg.Any<CancellationToken>());
        await _refreshTokenRepository.Received(1).RevokeAllUserTokensAsync(userId, Arg.Any<CancellationToken>());
        await _revokedUsersCache.Received(1).RevokeUserAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActivateUserShouldRestoreStatusAndUnrevokeUserFromCache()
    {
        var userId = Guid.NewGuid();
        var actingAdminId = Guid.NewGuid();

        _userRepository.IsActiveAsync(userId, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new ActivateUserCommandHandler(
            _userRepository,
            _revokedUsersCache,
            Substitute.For<ILogger<ActivateUserCommandHandler>>());

        await handler.Handle(new ActivateUserCommand(userId) { CurrentUserId = actingAdminId }, CancellationToken.None);

        await _userRepository.Received(1).SetUserActivationStatusAsync(userId, true, Arg.Any<CancellationToken>());
        await _revokedUsersCache.Received(1).UnrevokeUserAsync(userId, Arg.Any<CancellationToken>());
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
            _revokedUsersCache,
            Substitute.For<ILogger<DeleteUserCommandHandler>>());

        await handler.Handle(new DeleteUserCommand(userId) { CurrentUserId = actingAdminId }, CancellationToken.None);

        await _projectRepository.Received(1).EnsureManagerForProjectsAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { projectId })),
            actingAdminId,
            Arg.Any<CancellationToken>());
        await _identityService.Received(1).DeleteUserAsync(userId, Arg.Any<CancellationToken>());
        await _revokedUsersCache.Received(1).RevokeUserAsync(userId, Arg.Any<CancellationToken>());
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
            _revokedUsersCache,
            Substitute.For<ILogger<DeleteUserCommandHandler>>());

        await Should.ThrowAsync<CannotRemoveLastProjectManagerException>(() =>
            handler.Handle(new DeleteUserCommand(userId) { CurrentUserId = userId }, CancellationToken.None).AsTask());

        await _projectRepository.DidNotReceive().EnsureManagerForProjectsAsync(
            Arg.Any<IEnumerable<Guid>>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await _identityService.DidNotReceive().DeleteUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _revokedUsersCache.DidNotReceive().RevokeUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
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
            _revokedUsersCache,
            Substitute.For<ILogger<DeleteUserCommandHandler>>());

        await handler.Handle(new DeleteUserCommand(userId), CancellationToken.None);

        await _projectRepository.DidNotReceive().EnsureManagerForProjectsAsync(
            Arg.Any<IEnumerable<Guid>>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await _identityService.Received(1).DeleteUserAsync(userId, Arg.Any<CancellationToken>());
        await _revokedUsersCache.Received(1).RevokeUserAsync(userId, Arg.Any<CancellationToken>());
    }
}
