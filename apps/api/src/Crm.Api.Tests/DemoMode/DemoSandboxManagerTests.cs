using Crm.Api.DemoMode;
using Crm.Api.DemoMode.Sandbox;
using Crm.Infrastructure.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Crm.Api.Tests.DemoMode;

/// <summary>
/// Unit tests for <see cref="DemoSandboxManager"/>: creation, lookup, expiry, capacity and rate limits.
/// </summary>
public sealed class DemoSandboxManagerTests : IDisposable
{
    private readonly FakeSandboxDatabase _database = new();
    private readonly ManualTimeProvider _time = new();
    private readonly string _uploadRoot = Path.Combine(Path.GetTempPath(), "crm-demo-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_uploadRoot))
        {
            Directory.Delete(_uploadRoot, recursive: true);
        }
    }

    [Fact]
    public async Task CreateAsyncShouldCreateDatabaseAndReturnSandboxThatCanBeFound()
    {
        using var manager = CreateManager();

        var sandbox = await manager.CreateAsync(CancellationToken.None);

        sandbox.ShouldNotBeNull();
        sandbox.Id.Length.ShouldBe(32);
        _database.Databases.ContainsKey(sandbox.DatabaseName).ShouldBeTrue();
        sandbox.ConnectionString.ShouldContain(sandbox.DatabaseName);
        manager.TryGet(sandbox.Id, out var found).ShouldBeTrue();
        found.ShouldBeSameAs(sandbox);
        manager.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CreateAsyncShouldGiveEveryVisitorADifferentSandbox()
    {
        using var manager = CreateManager();

        var first = await manager.CreateAsync(CancellationToken.None);
        var second = await manager.CreateAsync(CancellationToken.None);

        first.Id.ShouldNotBe(second.Id);
        first.DatabaseName.ShouldNotBe(second.DatabaseName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0123456789abcdef0123456789abcdef")]
    public void TryGetWhenSandboxIsUnknownShouldReturnFalse(string? sandboxId)
    {
        using var manager = CreateManager();

        manager.TryGet(sandboxId, out var sandbox).ShouldBeFalse();
        sandbox.ShouldBeNull();
    }

    [Fact]
    public async Task TryGetShouldKeepAnActiveSandboxAliveBeyondTheIdleTimeout()
    {
        using var manager = CreateManager(o => o.IdleTimeoutMinutes = 30);
        var sandbox = await manager.CreateAsync(CancellationToken.None);

        _time.Advance(TimeSpan.FromMinutes(20));
        manager.TryGet(sandbox.Id, out _).ShouldBeTrue();
        _time.Advance(TimeSpan.FromMinutes(20));

        manager.TryGet(sandbox.Id, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task TryGetWhenSandboxWasIdleTooLongShouldReturnFalse()
    {
        using var manager = CreateManager(o => o.IdleTimeoutMinutes = 30);
        var sandbox = await manager.CreateAsync(CancellationToken.None);

        _time.Advance(TimeSpan.FromMinutes(31));

        manager.TryGet(sandbox.Id, out _).ShouldBeFalse();
    }

    [Fact]
    public async Task TryGetWhenSandboxReachedMaxLifetimeShouldReturnFalseEvenIfActive()
    {
        using var manager = CreateManager(o =>
        {
            o.IdleTimeoutMinutes = 30;
            o.MaxLifetimeMinutes = 60;
        });
        var sandbox = await manager.CreateAsync(CancellationToken.None);

        for (int i = 0; i < 3; i++)
        {
            _time.Advance(TimeSpan.FromMinutes(20));
            manager.TryGet(sandbox.Id, out _);
        }

        manager.TryGet(sandbox.Id, out _).ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveExpiredAsyncShouldDropOnlyExpiredSandboxes()
    {
        using var manager = CreateManager(o => o.IdleTimeoutMinutes = 30);
        var old = await manager.CreateAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(25));
        var fresh = await manager.CreateAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(10));

        int removed = await manager.RemoveExpiredAsync(CancellationToken.None);

        removed.ShouldBe(1);
        _database.Databases.ContainsKey(old.DatabaseName).ShouldBeFalse();
        _database.Databases.ContainsKey(fresh.DatabaseName).ShouldBeTrue();
        manager.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CreateAsyncWhenLimitReachedShouldEvictTheLeastRecentlyUsedIdleSandbox()
    {
        using var manager = CreateManager(o =>
        {
            o.MaxSandboxes = 2;
            o.EvictableAfterIdleSeconds = 120;
        });
        var first = await manager.CreateAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(60));
        var second = await manager.CreateAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(180));
        manager.TryGet(second.Id, out _).ShouldBeTrue();

        var third = await manager.CreateAsync(CancellationToken.None);

        third.ShouldNotBeNull();
        manager.Count.ShouldBe(2);
        manager.TryGet(first.Id, out _).ShouldBeFalse();
        manager.TryGet(second.Id, out _).ShouldBeTrue();
        _database.Databases.ContainsKey(first.DatabaseName).ShouldBeFalse();
    }

    [Fact]
    public async Task CreateAsyncWhenLimitReachedAndEveryoneIsActiveShouldReturnNull()
    {
        using var manager = CreateManager(o =>
        {
            o.MaxSandboxes = 2;
            o.EvictableAfterIdleSeconds = 120;
        });
        await manager.CreateAsync(CancellationToken.None);
        await manager.CreateAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(30));

        var third = await manager.CreateAsync(CancellationToken.None);

        third.ShouldBeNull();
        manager.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RemoveAsyncShouldDropDatabaseAndUploadedFiles()
    {
        using var manager = CreateManager();
        var sandbox = await manager.CreateAsync(CancellationToken.None);
        string uploads = Path.Combine(_uploadRoot, DemoSandboxPaths.SandboxesFolder, sandbox.Id, "users");
        Directory.CreateDirectory(uploads);
        await File.WriteAllTextAsync(Path.Combine(uploads, "avatar.png"), "x", TestContext.Current.CancellationToken);

        await manager.RemoveAsync(sandbox.Id, CancellationToken.None);

        _database.Databases.ContainsKey(sandbox.DatabaseName).ShouldBeFalse();
        Directory.Exists(Path.Combine(_uploadRoot, DemoSandboxPaths.SandboxesFolder, sandbox.Id)).ShouldBeFalse();
        manager.TryGet(sandbox.Id, out _).ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveAsyncWhenDropFailsShouldNotThrowAndShouldForgetTheSandbox()
    {
        using var manager = CreateManager();
        var sandbox = await manager.CreateAsync(CancellationToken.None);
        _database.FailDrops = true;

        await Should.NotThrowAsync(() => manager.RemoveAsync(sandbox.Id, CancellationToken.None));

        manager.TryGet(sandbox.Id, out _).ShouldBeFalse();
    }

    [Fact]
    public async Task InitializeAsyncShouldBuildTheTemplateAndRemoveLeftoverUploads()
    {
        using var manager = CreateManager();
        string leftovers = Path.Combine(_uploadRoot, DemoSandboxPaths.SandboxesFolder, "old", "users");
        Directory.CreateDirectory(leftovers);
        string shared = Path.Combine(_uploadRoot, "users");
        Directory.CreateDirectory(shared);

        await manager.InitializeAsync(CancellationToken.None);

        _database.TemplatePreparations.ShouldBe(1);
        Directory.Exists(Path.Combine(_uploadRoot, DemoSandboxPaths.SandboxesFolder)).ShouldBeFalse();
        Directory.Exists(shared).ShouldBeTrue();
    }

    [Fact]
    public void TryAcquireCreationPermitShouldLimitNewSandboxesPerAddress()
    {
        using var manager = CreateManager(o => o.NewSandboxesPerAddress = 2);

        manager.TryAcquireCreationPermit("203.0.113.7").ShouldBeTrue();
        manager.TryAcquireCreationPermit("203.0.113.7").ShouldBeTrue();
        manager.TryAcquireCreationPermit("203.0.113.7").ShouldBeFalse();
        manager.TryAcquireCreationPermit("203.0.113.8").ShouldBeTrue();
    }

    [Fact]
    public void TryReserveUploadShouldEnforceFileCountAndTotalSize()
    {
        var sandbox = new DemoSandbox("id", "db", "cs", _time.GetUtcNow());

        sandbox.TryReserveUpload(600, maxFiles: 2, maxBytes: 1000).ShouldBeTrue();
        sandbox.TryReserveUpload(600, maxFiles: 2, maxBytes: 1000).ShouldBeFalse();
        sandbox.TryReserveUpload(300, maxFiles: 2, maxBytes: 1000).ShouldBeTrue();
        sandbox.TryReserveUpload(1, maxFiles: 2, maxBytes: 1000).ShouldBeFalse();

        sandbox.ReleaseUpload(300);
        sandbox.TryReserveUpload(300, maxFiles: 2, maxBytes: 1000).ShouldBeTrue();
    }

    private DemoSandboxManager CreateManager(Action<DemoSandboxOptions>? configure = null)
    {
        var options = new DemoModeOptions { Enabled = true };
        configure?.Invoke(options.Sandbox);

        return new DemoSandboxManager(
            _database,
            MsOptions.Create(options),
            MsOptions.Create(new FileStorageOptions { UploadDirectory = _uploadRoot, BaseUrl = "http://localhost" }),
            _time,
            NullLogger<DemoSandboxManager>.Instance);
    }
}
