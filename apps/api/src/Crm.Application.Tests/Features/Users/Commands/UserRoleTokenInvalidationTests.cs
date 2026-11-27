using Crm.Application.Dtos.Roles;
using Crm.Application.Features.Users.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.Users.Commands;

/// <summary>
/// Unit tests verifying that assigning or removing user roles invalidates prior access tokens
/// via <see cref="IRevokedUsersCache.RevokeTokensIssuedBeforeAsync"/>.
/// </summary>
public class UserRoleTokenInvalidationTests
{
    private readonly IAuthRoleRepository _roleRepository = Substitute.For<IAuthRoleRepository>();
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly IRevokedUsersCache _revokedUsersCache = Substitute.For<IRevokedUsersCache>();

    [Fact]
    public async Task AssignUserRoleShouldInvalidatePriorAccessTokensAfterRoleAssignment()
    {
        var userId = Guid.CreateVersion7();
        var roleId = Guid.CreateVersion7();

        _roleRepository.GetAsync<RoleUserDto>(roleId, Arg.Any<CancellationToken>())
            .Returns(new RoleUserDto(roleId, "Coordinator"));

        var handler = new AssignUserRoleCommandHandler(
            _roleRepository,
            _identityService,
            _revokedUsersCache,
            Substitute.For<ILogger<AssignUserRoleCommandHandler>>());

        await handler.Handle(new AssignUserRoleCommand(userId, roleId), TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            _identityService.AddToRoleAsync(userId, "Coordinator", Arg.Any<CancellationToken>());
            _revokedUsersCache.RevokeTokensIssuedBeforeAsync(userId, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task RemoveUserRoleShouldInvalidatePriorAccessTokensAfterRoleRemoval()
    {
        var userId = Guid.CreateVersion7();
        var roleId = Guid.CreateVersion7();

        _roleRepository.GetAsync<RoleUserDto>(roleId, Arg.Any<CancellationToken>())
            .Returns(new RoleUserDto(roleId, RoleConstants.Admin));
        _roleRepository.IsUserLastAdminAsync(userId, Arg.Any<CancellationToken>()).Returns(false);

        var handler = new RemoveUserRoleCommandHandler(
            _identityService,
            _roleRepository,
            _revokedUsersCache,
            Substitute.For<ILogger<RemoveUserRoleCommandHandler>>());

        await handler.Handle(new RemoveUserRoleCommand(userId, roleId), TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            _identityService.RemoveFromRoleAsync(userId, RoleConstants.Admin, Arg.Any<CancellationToken>());
            _revokedUsersCache.RevokeTokensIssuedBeforeAsync(userId, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task RemoveUserRoleWhenLastAdminShouldThrowWithoutInvalidatingTokens()
    {
        var userId = Guid.CreateVersion7();
        var roleId = Guid.CreateVersion7();

        _roleRepository.GetAsync<RoleUserDto>(roleId, Arg.Any<CancellationToken>())
            .Returns(new RoleUserDto(roleId, RoleConstants.Admin));
        _roleRepository.IsUserLastAdminAsync(userId, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new RemoveUserRoleCommandHandler(
            _identityService,
            _roleRepository,
            _revokedUsersCache,
            Substitute.For<ILogger<RemoveUserRoleCommandHandler>>());

        await Should.ThrowAsync<DomainValidationException>(async () =>
            await handler.Handle(new RemoveUserRoleCommand(userId, roleId), TestContext.Current.CancellationToken));

        await _revokedUsersCache.DidNotReceiveWithAnyArgs()
            .RevokeTokensIssuedBeforeAsync(default, TestContext.Current.CancellationToken);
    }
}
