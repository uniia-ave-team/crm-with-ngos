using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.DemoMode;
using Crm.Api.DemoMode.Sandbox;
using Crm.Application.Dtos.Auth;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Crm.Api.Tests.DemoMode;

/// <summary>
/// Runs the real API in demo mode against a real PostgreSQL and walks through a visitor's journey:
/// every visitor gets a private copy of the demo data, and the shared data never changes.
/// </summary>
/// <param name="fixture">The running API and database.</param>
public sealed class DemoModeEndToEndTests(DemoModeEndToEndTests.DemoApiFixture fixture)
    : IClassFixture<DemoModeEndToEndTests.DemoApiFixture>
{
    private static readonly DemoAccountOptions Admin = DemoAccountOptions.Defaults[0];

    [Fact]
    public async Task StatusShouldAnnounceDemoModeAndItsAccounts()
    {
        SkipWithoutDocker();
        using var client = fixture.CreateClient();

        using var status = await ReadJsonAsync(await client.GetAsync("/api/v1/system/status", TestContext.Current.CancellationToken));

        status.RootElement.GetProperty("isSetupComplete").GetBoolean().ShouldBeTrue();
        var demo = status.RootElement.GetProperty("demo");
        demo.GetProperty("accounts").GetArrayLength().ShouldBe(DemoAccountOptions.Defaults.Count);
        demo.GetProperty("accounts")[0].GetProperty("email").GetString().ShouldBe(Admin.Email);
        demo.GetProperty("sessionMinutes").GetInt32().ShouldBe(60);
    }

    [Fact]
    public async Task EveryVisitorShouldWorkInAPrivateCopyOfTheDemoData()
    {
        SkipWithoutDocker();
        var ct = TestContext.Current.CancellationToken;
        string roleName = "Тестова роль " + Guid.NewGuid().ToString("N")[..8];

        // Visitor A signs in and changes something.
        using var visitorA = fixture.CreateClient();
        var tokensA = await SignInAsync(visitorA, Admin);
        string sandboxA = CurrentSandboxId(visitorA);
        long expiresAtA = ReadSandboxExpiry(tokensA.AccessToken);
        DateTimeOffset.FromUnixTimeSeconds(expiresAtA).ShouldBeInRange(
            DateTimeOffset.UtcNow.AddMinutes(58),
            DateTimeOffset.UtcNow.AddMinutes(61));
        var created = await visitorA.PostAsJsonAsync("/api/v1/roles", new { name = roleName }, ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        (await ListRolesAsync(visitorA)).ShouldContain(roleName);

        // Visitor B signs in with the same public account and sees untouched demo data.
        using var visitorB = fixture.CreateClient();
        await SignInAsync(visitorB, Admin);
        CurrentSandboxId(visitorB).ShouldNotBe(sandboxA);
        (await ListRolesAsync(visitorB)).ShouldNotContain(roleName);

        // The shared database, the source of every sandbox, did not change either.
        (await fixture.CountRolesInSharedDatabaseAsync(roleName)).ShouldBe(0);

        // Refreshing tokens keeps visitor A in the same sandbox.
        var refreshed = await visitorA.PostAsJsonAsync("/api/v1/auth/refresh-token", tokensA, ct);
        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var newTokens = (await refreshed.Content.ReadFromJsonAsync<AuthTokensDto>(ct)).ShouldNotBeNull();
        visitorA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newTokens.AccessToken);
        ReadSandboxExpiry(newTokens.AccessToken).ShouldBe(expiresAtA);
        (await ListRolesAsync(visitorA)).ShouldContain(roleName);

        // Signing in again from the same browser (the cookie) continues in the same sandbox.
        visitorA.DefaultRequestHeaders.Authorization = null;
        await SignInAsync(visitorA, DemoAccountOptions.Defaults[1]);
        var tokensAgain = await SignInAsync(visitorA, Admin);
        CurrentSandboxId(visitorA).ShouldBe(sandboxA);
        ReadSandboxExpiry(tokensAgain.AccessToken).ShouldBe(expiresAtA);
        (await ListRolesAsync(visitorA)).ShouldContain(roleName);
    }

    [Fact]
    public async Task ActionsThatLeaveTheSessionShouldBeBlocked()
    {
        SkipWithoutDocker();
        var ct = TestContext.Current.CancellationToken;
        using var visitor = fixture.CreateClient();
        await SignInAsync(visitor, Admin);

        var invitation = await visitor.PostAsJsonAsync("/api/v1/invitations", new { email = "new@demo.example.org" }, ct);
        var firstAdmin = await fixture.CreateClient().PostAsJsonAsync("/api/v1/users", new { email = "x@demo.example.org" }, ct);

        invitation.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ReadErrorCodeAsync(invitation)).ShouldBe(DemoErrorCodes.Restricted);
        firstAdmin.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ReadErrorCodeAsync(firstAdmin)).ShouldBe(DemoErrorCodes.Restricted);
    }

    [Fact]
    public async Task ExpiredSandboxShouldEndTheSessionAndItsDatabase()
    {
        SkipWithoutDocker();
        var ct = TestContext.Current.CancellationToken;
        using var visitor = fixture.CreateClient();
        var tokens = await SignInAsync(visitor, Admin);
        string sandboxId = CurrentSandboxId(visitor);
        fixture.SandboxManager.TryGet(sandboxId, out var sandbox).ShouldBeTrue();

        await fixture.SandboxManager.RemoveAsync(sandboxId, ct);

        var request = await visitor.GetAsync("/api/v1/users/me", ct);
        var refresh = await visitor.PostAsJsonAsync("/api/v1/auth/refresh-token", tokens, ct);
        request.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ReadErrorCodeAsync(request)).ShouldBe(DemoErrorCodes.SessionExpired);
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ReadErrorCodeAsync(refresh)).ShouldBe(DemoErrorCodes.SessionExpired);
        (await fixture.DatabaseExistsAsync(sandbox.DatabaseName)).ShouldBeFalse();
    }

    [Fact]
    public async Task DisablingOrDeletingAColleagueShouldSignThemOutOnlyInThatSandbox()
    {
        SkipWithoutDocker();
        var ct = TestContext.Current.CancellationToken;
        var coordinator = DemoAccountOptions.Defaults[1];

        // Visitor A signs in as the coordinator and then, in the same browser, as the administrator.
        using var visitorA = fixture.CreateClient();
        var coordinatorTokensA = await SignInAsync(visitorA, coordinator);
        await SignInAsync(visitorA, Admin);
        Guid coordinatorId = ReadUserId(coordinatorTokensA.AccessToken);
        using var coordinatorA = fixture.CreateClient();
        coordinatorA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", coordinatorTokensA.AccessToken);

        // Visitor B works as the same coordinator (the same user id) in a sandbox of their own.
        using var visitorB = fixture.CreateClient();
        await SignInAsync(visitorB, coordinator);

        (await visitorA.PostAsync($"/api/v1/users/{coordinatorId}/deactivate", content: null, ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await coordinatorA.GetAsync("/api/v1/users/me", ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await visitorB.GetAsync("/api/v1/users/me", ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await visitorA.PostAsync($"/api/v1/users/{coordinatorId}/activate", content: null, ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await coordinatorA.GetAsync("/api/v1/users/me", ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await visitorA.DeleteAsync($"/api/v1/users/{coordinatorId}", ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await coordinatorA.GetAsync("/api/v1/users/me", ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await visitorB.GetAsync("/api/v1/users/me", ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangingTheRolesOfAColleagueShouldEndTheirOlderTokensOnlyInThatSandbox()
    {
        SkipWithoutDocker();
        var ct = TestContext.Current.CancellationToken;
        var coordinator = DemoAccountOptions.Defaults[1];

        // Visitor A signs in as the coordinator and then, in the same browser, as the administrator.
        using var visitorA = fixture.CreateClient();
        var coordinatorTokensA = await SignInAsync(visitorA, coordinator);
        await SignInAsync(visitorA, Admin);
        Guid coordinatorId = ReadUserId(coordinatorTokensA.AccessToken);
        using var coordinatorA = fixture.CreateClient();
        coordinatorA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", coordinatorTokensA.AccessToken);

        // Visitor B works as the same coordinator (the same user id) in a sandbox of their own.
        using var visitorB = fixture.CreateClient();
        await SignInAsync(visitorB, coordinator);

        Guid volunteerRoleId = await FindRoleIdAsync(visitorA, DemoAccountOptions.VolunteerRole);
        (await visitorA.PostAsync($"/api/v1/users/{coordinatorId}/roles/{volunteerRoleId}", content: null, ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // The token issued before the change no longer works in sandbox A, a refreshed one does.
        (await coordinatorA.GetAsync("/api/v1/users/me", ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        coordinatorA.DefaultRequestHeaders.Authorization = null;
        var refreshed = await coordinatorA.PostAsJsonAsync("/api/v1/auth/refresh-token", coordinatorTokensA, ct);
        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var newTokens = (await refreshed.Content.ReadFromJsonAsync<AuthTokensDto>(ct)).ShouldNotBeNull();
        coordinatorA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newTokens.AccessToken);
        (await coordinatorA.GetAsync("/api/v1/users/me", ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // In sandbox B nothing changed for the same user.
        (await visitorB.GetAsync("/api/v1/users/me", ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MaintenanceNoticeShouldReachVisitorsFromTheSharedDatabase()
    {
        SkipWithoutDocker();
        var ct = TestContext.Current.CancellationToken;
        using var visitor = fixture.CreateClient();
        await SignInAsync(visitor, Admin);
        string message = "Демо оновиться о 18:00 " + Guid.NewGuid().ToString("N")[..8];

        // `crm notice` writes to the shared database; the visitor's sandbox was copied before that.
        await fixture.SetMaintenanceNoticeAsync(message);

        try
        {
            using var status = await ReadJsonAsync(await visitor.GetAsync("/api/v1/system/status", ct));
            status.RootElement.GetProperty("notice").GetProperty("message").GetString().ShouldBe(message);

            using var anonymousClient = fixture.CreateClient();
            using var anonymousStatus = await ReadJsonAsync(await anonymousClient.GetAsync("/api/v1/system/status", ct));
            anonymousStatus.RootElement.GetProperty("notice").GetProperty("message").GetString().ShouldBe(message);
        }
        finally
        {
            await fixture.ClearMaintenanceNoticeAsync();
        }
    }

    [Fact]
    public async Task VisitorChangesShouldLeaveNoAuditLogInTheSharedDatabase()
    {
        SkipWithoutDocker();
        var ct = TestContext.Current.CancellationToken;
        using var visitor = fixture.CreateClient();
        await SignInAsync(visitor, Admin);

        var created = await visitor.PostAsJsonAsync("/api/v1/roles", new { name = "Аудит " + Guid.NewGuid().ToString("N")[..8] }, ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        // The audit log is written by a background service; the fixture makes it flush every 50 ms.
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        (await fixture.CountAuditLogsInSharedDatabaseAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task FailedSignInShouldNotCreateASandbox()
    {
        SkipWithoutDocker();
        using var client = fixture.CreateClient();
        int before = fixture.SandboxManager.Count;

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = Admin.Email, password = "Wrong-password-1" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        fixture.SandboxManager.Count.ShouldBe(before);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return document.RootElement.GetProperty("errorCode").GetString();
    }

    private static long ReadSandboxExpiry(string accessToken) =>
        long.Parse(
            new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
                .ReadJwtToken(accessToken).Claims.Single(c => c.Type == DemoClaimTypes.SandboxExpiresAt).Value,
            System.Globalization.CultureInfo.InvariantCulture);

    private static Guid ReadUserId(string accessToken) =>
        Guid.Parse(
            new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
                .ReadJwtToken(accessToken).Subject);

    private static string CurrentSandboxId(HttpClient client) =>
        client.DefaultRequestHeaders.GetValues(DemoApiFixture.SandboxHeader).Single();

    private static async Task<string> ListRolesAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/roles?pageSize=100", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Guid> FindRoleIdAsync(HttpClient client, string roleName)
    {
        using var roles = JsonDocument.Parse(await ListRolesAsync(client));

        return roles.RootElement.GetProperty("items").EnumerateArray()
            .Where(role => role.GetProperty("name").GetString() == roleName)
            .Select(role => role.GetProperty("id").GetGuid())
            .Single();
    }

    private static async Task<AuthTokensDto> SignInAsync(HttpClient client, DemoAccountOptions account)
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = account.Email, password = account.Password }, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var tokens = await response.Content.ReadFromJsonAsync<AuthTokensDto>(ct);
        tokens.ShouldNotBeNull();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        // Remember the sandbox of this client for the tests that look inside the manager.
        string sandboxId = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(tokens.AccessToken).Claims.Single(c => c.Type == DemoClaimTypes.SandboxId).Value;
        client.DefaultRequestHeaders.Remove(DemoApiFixture.SandboxHeader);
        client.DefaultRequestHeaders.Add(DemoApiFixture.SandboxHeader, sandboxId);

        return tokens;
    }

    private void SkipWithoutDocker()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
        }
    }

    /// <summary>
    /// Starts PostgreSQL in Docker and the API in demo mode on top of it.
    /// </summary>
    public sealed class DemoApiFixture : IAsyncLifetime
    {
        /// <summary>
        /// A header the tests use to remember which sandbox a client works in. The API ignores it.
        /// </summary>
        public const string SandboxHeader = "X-Test-Sandbox";

        private readonly string _uploadDirectory = Path.Combine(Path.GetTempPath(), "crm-demo-e2e-" + Guid.NewGuid().ToString("N"));
        private PostgreSqlContainer? _container;
        private WebApplicationFactory<Program>? _factory;

        /// <summary>
        /// Gets a value indicating whether Docker was available and the API started.
        /// </summary>
        public bool IsDockerAvailable { get; private set; }

        /// <summary>
        /// Gets the sandbox manager of the running API.
        /// </summary>
        public DemoSandboxManager SandboxManager =>
            (_factory ?? throw new InvalidOperationException("The API is not running.")).Services.GetRequiredService<DemoSandboxManager>();

        /// <inheritdoc />
        public async ValueTask InitializeAsync()
        {
            try
            {
                _container = new PostgreSqlBuilder("postgres:17-alpine")
                    .WithDatabase("crm")
                    .WithCommand("-c", "max_connections=200")
                    .Build();

                await _container.StartAsync();
            }
#pragma warning disable CA1031 // Any failure to start the container means Docker is unavailable: the tests are skipped.
            catch (Exception)
#pragma warning restore CA1031
            {
                return;
            }

            // Demo mode is wired while services are registered, so the settings must be visible to Program
            // from the start. UseSetting provides that without process-wide environment variables, which
            // would leak into other API tests running in parallel.
            string connectionString = _container.GetConnectionString();
            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
                .UseSetting("environment", "Production")
                .UseSetting("ConnectionStrings:PostgreSqlConnection", connectionString)
                .UseSetting("JwtOptions:Secret", "demo-e2e-tests-jwt-secret-0123456789-0123456789")
                .UseSetting("DemoMode:Enabled", "true")
                .UseSetting("DemoMode:Sandbox:NewSandboxesPerAddress", "1000")
                .UseSetting("FileStorage:UploadDirectory", _uploadDirectory)
                .UseSetting("Auditing:FlushIntervalMilliseconds", "50"));

            // Starts the host: migrations, seeding and the sandbox template.
            _ = _factory.Server;

            IsDockerAvailable = true;
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (_factory is not null)
            {
                await _factory.DisposeAsync();
            }

            if (_container is not null)
            {
                await _container.DisposeAsync();
            }

            if (Directory.Exists(_uploadDirectory))
            {
                Directory.Delete(_uploadDirectory, recursive: true);
            }
        }

        /// <summary>
        /// Creates a client that behaves like a separate browser: it keeps its own cookies.
        /// </summary>
        /// <returns>The HTTP client.</returns>
        public HttpClient CreateClient() =>
            (_factory ?? throw new InvalidOperationException("The API is not running."))
                .CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        /// <summary>
        /// Counts the roles with a given name in the shared database every sandbox is copied from.
        /// </summary>
        /// <param name="roleName">The role name.</param>
        /// <returns>The number of matching roles.</returns>
        public async Task<long> CountRolesInSharedDatabaseAsync(string roleName)
        {
            await using var connection = new NpgsqlConnection(_container!.GetConnectionString());
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM \"AuthRoles\" WHERE \"Name\" = @name", connection);
            command.Parameters.AddWithValue("name", roleName);

            return (long)(await command.ExecuteScalarAsync())!;
        }

        /// <summary>
        /// Sets the maintenance notice the way <c>crm notice</c> does: in the shared database, outside any request.
        /// Also empties the API's memory cache, which keeps the notice for 30 seconds.
        /// </summary>
        /// <param name="message">The notice text.</param>
        /// <returns>A task that completes when the notice is stored.</returns>
        public async Task SetMaintenanceNoticeAsync(string message)
        {
            var services = (_factory ?? throw new InvalidOperationException("The API is not running.")).Services;
            var now = DateTime.UtcNow;

            await using (var scope = services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<IMaintenanceNoticeRepository>()
                    .UpsertAsync(message, now, now.AddHours(1), now);
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
            }

            ClearMemoryCache(services);
        }

        /// <summary>
        /// Removes the maintenance notice from the shared database and the API's memory cache.
        /// </summary>
        /// <returns>A task that completes when the notice is gone.</returns>
        public async Task ClearMaintenanceNoticeAsync()
        {
            var services = (_factory ?? throw new InvalidOperationException("The API is not running.")).Services;

            await using (var scope = services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<IMaintenanceNoticeRepository>().ClearAsync();
            }

            ClearMemoryCache(services);
        }

        /// <summary>
        /// Counts the audit log records in the shared database every sandbox is copied from.
        /// </summary>
        /// <returns>The number of audit log records.</returns>
        public async Task<long> CountAuditLogsInSharedDatabaseAsync()
        {
            await using var connection = new NpgsqlConnection(_container!.GetConnectionString());
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM \"AuditLogs\"", connection);

            return (long)(await command.ExecuteScalarAsync())!;
        }

        /// <summary>
        /// Checks whether a database exists on the server.
        /// </summary>
        /// <param name="databaseName">The database name.</param>
        /// <returns><c>true</c> when the database exists.</returns>
        public async Task<bool> DatabaseExistsAsync(string databaseName)
        {
            await using var connection = new NpgsqlConnection(_container!.GetConnectionString());
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM pg_database WHERE datname = @name", connection);
            command.Parameters.AddWithValue("name", databaseName);

            return (long)(await command.ExecuteScalarAsync())! > 0;
        }

        private static void ClearMemoryCache(IServiceProvider services)
        {
            if (services.GetRequiredService<IMemoryCache>() is MemoryCache memoryCache)
            {
                memoryCache.Clear();
            }
        }
    }
}
