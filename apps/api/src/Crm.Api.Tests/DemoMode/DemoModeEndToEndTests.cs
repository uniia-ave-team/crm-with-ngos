using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.DemoMode;
using Crm.Api.DemoMode.Sandbox;
using Crm.Application.Dtos.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
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
        var newTokens = await refreshed.Content.ReadFromJsonAsync<AuthTokensDto>(ct);
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

    private static string CurrentSandboxId(HttpClient client) =>
        client.DefaultRequestHeaders.GetValues(DemoApiFixture.SandboxHeader).Single();

    private static async Task<string> ListRolesAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/roles?pageSize=100", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
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

        private static readonly string[] Variables =
        [
            "ConnectionStrings__PostgreSqlConnection",
            "JwtOptions__Secret",
            "DemoMode__Enabled",
            "DemoMode__Sandbox__NewSandboxesPerAddress",
            "FileStorage__UploadDirectory",
        ];

        private readonly Dictionary<string, string?> _previousVariables = [];
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

            // Demo mode is wired while services are registered, before a test host could inject settings,
            // so the configuration goes through environment variables.
            SetVariable("ConnectionStrings__PostgreSqlConnection", _container.GetConnectionString());
            SetVariable("JwtOptions__Secret", "demo-e2e-tests-jwt-secret-0123456789-0123456789");
            SetVariable("DemoMode__Enabled", "true");
            SetVariable("DemoMode__Sandbox__NewSandboxesPerAddress", "1000");
            SetVariable("FileStorage__UploadDirectory", _uploadDirectory);

            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("environment", "Production"));

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

            foreach (var (name, value) in _previousVariables)
            {
                Environment.SetEnvironmentVariable(name, value);
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

        private void SetVariable(string name, string value)
        {
            if (Array.IndexOf(Variables, name) < 0)
            {
                throw new ArgumentException($"Unknown variable {name}.", nameof(name));
            }

            _previousVariables[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}
