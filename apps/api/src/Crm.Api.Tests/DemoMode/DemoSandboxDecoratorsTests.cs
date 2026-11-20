using Crm.Api.DemoMode;
using Crm.Api.DemoMode.Sandbox;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Crm.Api.Tests.DemoMode;

/// <summary>
/// Unit tests for the services that demo mode wraps per sandbox: file storage, the role permission cache,
/// the access token claims and the write guard of the shared database.
/// </summary>
public sealed class DemoSandboxDecoratorsTests
{
    private const string Folder = "users";

    private readonly DemoSandbox _sandbox = new("abc123", "crm_demo_abc123", "Host=localhost;Database=crm_demo_abc123", DateTimeOffset.UtcNow);
    private readonly IDemoSandboxAccessor _accessor = Substitute.For<IDemoSandboxAccessor>();
    private readonly IFileStorageService _innerStorage = Substitute.For<IFileStorageService>();
    private readonly IRolePermissionsCache _innerCache = Substitute.For<IRolePermissionsCache>();
    private readonly DemoModeOptions _options = new() { Enabled = true };

    private string SandboxFolder => DemoSandboxPaths.GetSandboxFolder(_sandbox.Id, Folder);

    [Fact]
    public async Task SaveInSandboxShouldStoreTheFileInTheSandboxFolder()
    {
        UseSandbox();
        using var stream = new MemoryStream(new byte[10]);
        _innerStorage.SaveImageAsync(stream, "a.png", SandboxFolder, Arg.Any<CancellationToken>()).Returns("saved.png");

        string saved = await CreateStorage().SaveImageAsync(stream, "a.png", Folder, TestContext.Current.CancellationToken);

        saved.ShouldBe("saved.png");
        await _innerStorage.DidNotReceive().SaveImageAsync(Arg.Any<Stream>(), Arg.Any<string>(), Folder, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveOverTheSandboxQuotaShouldBeRejectedWithQuotaCode()
    {
        UseSandbox();
        _options.Sandbox.MaxUploadFiles = 1;
        var storage = CreateStorage();
        using var stream = new MemoryStream(new byte[10]);
        await storage.SaveFileAsync(stream, "a.txt", Folder, TestContext.Current.CancellationToken);

        var exception = await Should.ThrowAsync<DemoModeException>(
            () => storage.SaveFileAsync(stream, "b.txt", Folder, TestContext.Current.CancellationToken));

        exception.ErrorCode.ShouldBe(DemoErrorCodes.QuotaExceeded);
        exception.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task FailedSaveShouldNotCountAgainstTheQuota()
    {
        UseSandbox();
        _options.Sandbox.MaxUploadFiles = 1;
        var storage = CreateStorage();
        using var stream = new MemoryStream(new byte[10]);
        _innerStorage.SaveFileAsync(stream, "bad.txt", SandboxFolder, Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new ArgumentException("invalid"));

        await Should.ThrowAsync<ArgumentException>(() => storage.SaveFileAsync(stream, "bad.txt", Folder, TestContext.Current.CancellationToken));

        _innerStorage.SaveFileAsync(stream, "ok.txt", SandboxFolder, Arg.Any<CancellationToken>()).Returns("ok");
        (await storage.SaveFileAsync(stream, "ok.txt", Folder, TestContext.Current.CancellationToken)).ShouldBe("ok");
    }

    [Fact]
    public async Task SaveFromRequestWithoutSandboxShouldBeRejected()
    {
        _accessor.IsHttpRequest.Returns(true);
        using var stream = new MemoryStream(new byte[10]);

        await Should.ThrowAsync<DemoModeException>(
            () => CreateStorage().SaveFileAsync(stream, "a.txt", Folder, TestContext.Current.CancellationToken));

        await _innerStorage.DidNotReceiveWithAnyArgs().SaveFileAsync(default, default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SaveOutsideHttpRequestsShouldGoToTheSharedFolder()
    {
        using var stream = new MemoryStream(new byte[10]);
        _innerStorage.SaveFileAsync(stream, "seed.txt", Folder, Arg.Any<CancellationToken>()).Returns("seeded");

        (await CreateStorage().SaveFileAsync(stream, "seed.txt", Folder, TestContext.Current.CancellationToken)).ShouldBe("seeded");
    }

    [Fact]
    public async Task GetInSandboxShouldFallBackToTheSharedFile()
    {
        UseSandbox();
        using var shared = new MemoryStream();
        _innerStorage.GetFileAsync("logo.png", SandboxFolder, Arg.Any<CancellationToken>())
            .Returns<Task<Stream>>(_ => throw new FileNotFoundException());
        _innerStorage.GetFileAsync("logo.png", Folder, Arg.Any<CancellationToken>()).Returns(shared);

        var stream = await CreateStorage().GetFileAsync("logo.png", Folder, TestContext.Current.CancellationToken);

        stream.ShouldBeSameAs(shared);
    }

    [Fact]
    public async Task DeleteInSandboxShouldNeverTouchTheSharedFile()
    {
        UseSandbox();

        await CreateStorage().DeleteFileAsync("logo.png", Folder, TestContext.Current.CancellationToken);

        await _innerStorage.Received(1).DeleteFileAsync("logo.png", SandboxFolder, Arg.Any<CancellationToken>());
        await _innerStorage.DidNotReceive().DeleteFileAsync(Arg.Any<string>(), Folder, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteFromRequestWithoutSandboxShouldDoNothing()
    {
        _accessor.IsHttpRequest.Returns(true);

        await CreateStorage().DeleteFileAsync("logo.png", Folder, TestContext.Current.CancellationToken);

        await _innerStorage.DidNotReceiveWithAnyArgs().DeleteFileAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RolePermissionsInSandboxShouldComeFromTheSandboxDatabaseAndStayThere()
    {
        UseSandbox();
        var roleId = Guid.NewGuid();
        var otherRoleId = Guid.NewGuid();
        var repository = Substitute.For<IAuthRoleRepository>();
        repository.GetAllRolesPermissionsAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, HashSet<string>> { [roleId] = ["users.view"] });
        var cache = CreateCache(repository);

        var loaded = await cache.GetRolePermissionsAsync(roleId, TestContext.Current.CancellationToken);
        await cache.SetRolePermissionsAsync(otherRoleId, ["users.delete"], TestContext.Current.CancellationToken);
        var changed = await cache.GetRolePermissionsAsync(otherRoleId, TestContext.Current.CancellationToken);
        await cache.RemoveRolePermissionsAsync(roleId, TestContext.Current.CancellationToken);
        var removed = await cache.GetRolePermissionsAsync(roleId, TestContext.Current.CancellationToken);

        loaded.ShouldBe(["users.view"]);
        changed.ShouldBe(["users.delete"]);
        removed.ShouldBeEmpty();
        await repository.Received(1).GetAllRolesPermissionsAsync(Arg.Any<CancellationToken>());
        await _innerCache.DidNotReceiveWithAnyArgs().GetRolePermissionsAsync(default, TestContext.Current.CancellationToken);
        await _innerCache.DidNotReceiveWithAnyArgs().SetRolePermissionsAsync(default, default, TestContext.Current.CancellationToken);
        await _innerCache.DidNotReceiveWithAnyArgs().RemoveRolePermissionsAsync(default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RolePermissionsWithoutSandboxShouldUseTheRegularCache()
    {
        var roleId = Guid.NewGuid();
        IReadOnlySet<string> rights = new HashSet<string> { "users.view" };
        _innerCache.GetRolePermissionsAsync(roleId, Arg.Any<CancellationToken>()).Returns(rights);

        var result = await CreateCache(Substitute.For<IAuthRoleRepository>())
            .GetRolePermissionsAsync(roleId, TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(rights);
    }

    [Fact]
    public void ClaimsProviderShouldNameTheSandboxOnlyInsideOne()
    {
        var provider = new DemoSandboxClaimsProvider(_accessor);

        provider.GetClaims(null).ShouldBeEmpty();

        UseSandbox();
        var claim = provider.GetClaims(null).ShouldHaveSingleItem();
        claim.Type.ShouldBe(DemoClaimTypes.SandboxId);
        claim.Value.ShouldBe(_sandbox.Id);
    }

    [Theory]
    [InlineData("INSERT INTO \"Users\" VALUES (1)", true)]
    [InlineData("  update \"Users\" SET \"Name\" = 'x'", true)]
    [InlineData("SELECT 1; DELETE FROM \"Users\"", true)]
    [InlineData("DROP TABLE \"Users\"", true)]
    [InlineData("SELECT u.\"CreatedAt\", u.\"UpdatedAt\" FROM \"Users\" AS u", false)]
    [InlineData("SELECT COUNT(*) FROM \"Users\"", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsMutatingSqlShouldRecogniseWrites(string? sql, bool expected)
        => DemoDbWriteGuardInterceptor.IsMutatingSql(sql).ShouldBe(expected);

    [Fact]
    public void WriteGuardShouldBlockWritesOfRequestsWithoutSandboxOnly()
    {
        var state = Substitute.For<IDemoModeState>();
        state.IsActive.Returns(true);
        var guard = new DemoDbWriteGuardInterceptor(state, _accessor, NullLogger<DemoDbWriteGuardInterceptor>.Instance);
        using var write = new NpgsqlCommand("DELETE FROM \"Users\"");
        using var read = new NpgsqlCommand("SELECT 1");

        // Startup and background services: no HTTP request.
        Should.NotThrow(() => guard.NonQueryExecuting(write, null, default));

        _accessor.IsHttpRequest.Returns(true);
        Should.Throw<DemoModeException>(() => guard.NonQueryExecuting(write, null, default))
            .ErrorCode.ShouldBe(DemoErrorCodes.Restricted);
        Should.NotThrow(() => guard.ReaderExecuting(read, null, default));

        _accessor.Current.Returns(_sandbox);
        Should.NotThrow(() => guard.NonQueryExecuting(write, null, default));
    }

    private void UseSandbox()
    {
        _accessor.IsHttpRequest.Returns(true);
        _accessor.Current.Returns(_sandbox);
    }

    private DemoSandboxFileStorageService CreateStorage() =>
        new(_innerStorage, _accessor, MsOptions.Create(_options), NullLogger<DemoSandboxFileStorageService>.Instance);

    private DemoSandboxRolePermissionsCache CreateCache(IAuthRoleRepository repository)
    {
        var services = new ServiceCollection().AddSingleton(repository).BuildServiceProvider();
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(new DefaultHttpContext { RequestServices = services });

        return new DemoSandboxRolePermissionsCache(_innerCache, _accessor, httpContextAccessor);
    }
}
