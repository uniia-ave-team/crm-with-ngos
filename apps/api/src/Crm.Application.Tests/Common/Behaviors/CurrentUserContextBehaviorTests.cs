using Crm.Application.Common.Behaviors;
using Crm.Application.Common.Interfaces;
using Crm.Application.Interfaces;
using Mediator;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Common.Behaviors;

/// <summary>
/// Unit tests for <see cref="CurrentUserContextBehavior{TRequest, TResponse}"/> verifying automatic injection
/// and unconditional spoofing overwrite of current user ID and role IDs.
/// </summary>
public class CurrentUserContextBehaviorTests
{
    [Fact]
    public async Task HandleShouldPopulateAndOverwriteCurrentUserIdAndRoleIdsFromClaims()
    {
        var ct = TestContext.Current.CancellationToken;
        var verifiedUserId = Guid.NewGuid();
        List<Guid> verifiedRoles = [Guid.NewGuid(), Guid.NewGuid()];

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.GetUserId().Returns(verifiedUserId);
        currentUserService.GetRoleIds().Returns(verifiedRoles);

        var behavior = new CurrentUserContextBehavior<FullContextRequest, string>(currentUserService);
        var request = new FullContextRequest
        {
            CurrentUserId = Guid.NewGuid(),
            CurrentUserRoleIds = [Guid.NewGuid()],
        };

        var result = await behavior.Handle(
            request,
            (msg, _) => ValueTask.FromResult($"{msg.CurrentUserId}:{msg.CurrentUserRoleIds.Count}"),
            ct);

        request.CurrentUserId.ShouldBe(verifiedUserId);
        request.CurrentUserRoleIds.ShouldBe(verifiedRoles);
        result.ShouldBe($"{verifiedUserId}:2");
    }

    [Fact]
    public async Task HandleShouldOnlyCallGetUserIdForUserOnlyRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var verifiedUserId = Guid.NewGuid();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.GetUserId().Returns(verifiedUserId);

        var behavior = new CurrentUserContextBehavior<UserOnlyRequest, Guid>(currentUserService);
        var request = new UserOnlyRequest();

        var result = await behavior.Handle(
            request,
            (msg, _) => ValueTask.FromResult(msg.CurrentUserId),
            ct);

        result.ShouldBe(verifiedUserId);
        currentUserService.Received(1).GetUserId();
        currentUserService.DidNotReceive().GetRoleIds();
    }

    [Fact]
    public async Task HandleShouldOnlyCallGetRoleIdsForRolesOnlyRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        List<Guid> verifiedRoles = [Guid.NewGuid()];

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.GetRoleIds().Returns(verifiedRoles);

        var behavior = new CurrentUserContextBehavior<RolesOnlyRequest, int>(currentUserService);
        var request = new RolesOnlyRequest();

        var result = await behavior.Handle(
            request,
            (msg, _) => ValueTask.FromResult(msg.CurrentUserRoleIds.Count),
            ct);

        result.ShouldBe(1);
        currentUserService.DidNotReceive().GetUserId();
        currentUserService.Received(1).GetRoleIds();
    }

    [Fact]
    public async Task HandleShouldResolveUserIdOnceAndOverwriteWhenSelfTargeted()
    {
        var ct = TestContext.Current.CancellationToken;
        var verifiedUserId = Guid.NewGuid();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.GetUserId().Returns(verifiedUserId);

        var behavior = new CurrentUserContextBehavior<DualTargetRequest, (Guid Target, Guid Actor)>(currentUserService);
        var request = new DualTargetRequest
        {
            UserId = Guid.NewGuid(),
            CurrentUserId = Guid.NewGuid(),
            TargetCurrentUser = true,
        };

        var (target, actor) = await behavior.Handle(
            request,
            (msg, _) => ValueTask.FromResult((msg.UserId, msg.CurrentUserId)),
            ct);

        target.ShouldBe(verifiedUserId);
        actor.ShouldBe(verifiedUserId);
        currentUserService.Received(1).GetUserId();
    }

    [Fact]
    public async Task HandleShouldPreserveExplicitTargetUserIdWhenNotSelfTargeted()
    {
        var ct = TestContext.Current.CancellationToken;
        var verifiedActorId = Guid.NewGuid();
        var explicitTargetId = Guid.NewGuid();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.GetUserId().Returns(verifiedActorId);

        var behavior = new CurrentUserContextBehavior<DualTargetRequest, (Guid Target, Guid Actor)>(currentUserService);
        var request = new DualTargetRequest
        {
            UserId = explicitTargetId,
            TargetCurrentUser = false,
        };

        var (target, actor) = await behavior.Handle(
            request,
            (msg, _) => ValueTask.FromResult((msg.UserId, msg.CurrentUserId)),
            ct);

        target.ShouldBe(explicitTargetId);
        actor.ShouldBe(verifiedActorId);
        currentUserService.Received(1).GetUserId();
    }

    private sealed record FullContextRequest : IRequest<string>, ICurrentUserAware, ICurrentUserRolesAware
    {
        public Guid CurrentUserId { get; set; }

        public List<Guid> CurrentUserRoleIds { get; set; } = [];
    }

    private sealed record UserOnlyRequest : IRequest<Guid>, ICurrentUserAware
    {
        public Guid CurrentUserId { get; set; }
    }

    private sealed record RolesOnlyRequest : IRequest<int>, ICurrentUserRolesAware
    {
        public List<Guid> CurrentUserRoleIds { get; set; } = [];
    }

    private sealed record DualTargetRequest : IRequest<(Guid Target, Guid Actor)>, ICurrentUserAware, ISelfTargetableUserRequest
    {
        public Guid UserId { get; set; }

        public Guid CurrentUserId { get; set; }

        public bool TargetCurrentUser { get; init; }
    }
}
