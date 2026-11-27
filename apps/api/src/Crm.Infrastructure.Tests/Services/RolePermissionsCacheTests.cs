using Crm.Application.Interfaces;
using Crm.Domain.Exceptions;
using Crm.Infrastructure.Options;
using Crm.Infrastructure.Services.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Crm.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for <see cref="RolePermissionsCache"/> verifying <see cref="HybridCache"/> fallback loading,
/// negative caching for missing roles, explicit updates, and invalidation.
/// </summary>
public class RolePermissionsCacheTests
{
    [Fact]
    public async Task GetRolePermissionsShouldCachePermissionsAfterFirstFallbackLoad()
    {
        var ct = TestContext.Current.CancellationToken;
        var roleId = Guid.NewGuid();
        var identityService = Substitute.For<IRoleIdentityService>();
        identityService.GetRolePermissionsAsync(roleId, Arg.Any<CancellationToken>())
            .Returns(["ViewUser", "UpdateUser"]);

        using var serviceProvider = BuildServiceProvider(identityService);
        var cache = CreateCache(serviceProvider);

        var first = await cache.GetRolePermissionsAsync(roleId, ct);
        var second = await cache.GetRolePermissionsAsync(roleId, ct);

        first.ShouldContain("ViewUser");
        first.ShouldContain("UpdateUser");
        second.Count.ShouldBe(2);
        await identityService.Received(1).GetRolePermissionsAsync(roleId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRolePermissionsShouldNegativeCacheUnknownRoleOnNotFoundException()
    {
        var ct = TestContext.Current.CancellationToken;
        var missingRoleId = Guid.NewGuid();
        var identityService = Substitute.For<IRoleIdentityService>();
        identityService.GetRolePermissionsAsync(missingRoleId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TestNotFoundException());

        using var serviceProvider = BuildServiceProvider(identityService);
        var cache = CreateCache(serviceProvider);

        var first = await cache.GetRolePermissionsAsync(missingRoleId, ct);
        var second = await cache.GetRolePermissionsAsync(missingRoleId, ct);

        first.ShouldBeEmpty();
        second.ShouldBeEmpty();
        await identityService.Received(1).GetRolePermissionsAsync(missingRoleId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAndRemoveRolePermissionsShouldUpdateAndInvalidateCache()
    {
        var ct = TestContext.Current.CancellationToken;
        var roleId = Guid.NewGuid();
        var identityService = Substitute.For<IRoleIdentityService>();
        identityService.GetRolePermissionsAsync(roleId, Arg.Any<CancellationToken>())
            .Returns(["ReloadedRight"]);

        using var serviceProvider = BuildServiceProvider(identityService);
        var cache = CreateCache(serviceProvider);

        await cache.SetRolePermissionsAsync(roleId, ["ViewProject"], ct);
        var cached = await cache.GetRolePermissionsAsync(roleId, ct);
        cached.ShouldContain("ViewProject");
        await identityService.DidNotReceive().GetRolePermissionsAsync(roleId, Arg.Any<CancellationToken>());

        await cache.RemoveRolePermissionsAsync(roleId, ct);
        var reloaded = await cache.GetRolePermissionsAsync(roleId, ct);
        reloaded.ShouldContain("ReloadedRight");
        await identityService.Received(1).GetRolePermissionsAsync(roleId, Arg.Any<CancellationToken>());
    }

    private static ServiceProvider BuildServiceProvider(IRoleIdentityService identityService)
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        services.AddScoped(_ => identityService);
        return services.BuildServiceProvider();
    }

    private static RolePermissionsCache CreateCache(IServiceProvider serviceProvider)
        => new(
            serviceProvider.GetRequiredService<HybridCache>(),
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(new JwtOptions { AccessTokenExpiryMinutes = 15 }),
            NullLogger<RolePermissionsCache>.Instance);

    private sealed class TestNotFoundException() : NotFoundException("Role not found.");
}
