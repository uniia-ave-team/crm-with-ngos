using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Crm.Api.DemoMode;
using Crm.Api.DemoMode.Sandbox;
using Crm.Application.Interfaces;
using Crm.Infrastructure.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Crm.Api.Tests.DemoMode;

/// <summary>
/// Tests for <see cref="DemoModeMiddleware"/> in a small in-memory pipeline: which sandbox a request gets,
/// when a sandbox is created or removed, and which demo errors are returned.
/// </summary>
public sealed class DemoModeMiddlewareTests : IAsyncLifetime
{
    private const string SandboxHeader = "X-Test-Sandbox";
    private const string UserHeader = "X-Test-User";

    private readonly FakeSandboxDatabase _database = new();
    private readonly ManualTimeProvider _time = new();
    private readonly DemoModeOptions _options = new() { Enabled = true };
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private DemoSandboxManager? _manager;
    private IHost? _host;
    private HttpClient? _client;

    private DemoSandboxManager Manager => _manager ?? throw new InvalidOperationException("The test host is not started.");

    private HttpClient Client => _client ?? throw new InvalidOperationException("The test host is not started.");

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();

        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        _manager?.Dispose();
    }

    [Fact]
    public async Task AnonymousReadShouldPassWithoutSandbox()
    {
        await StartAsync();

        var response = await Client.GetAsync("/open", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("none");
        Manager.Count.ShouldBe(0);
    }

    [Fact]
    public async Task BlockedEndpointShouldReturn403WithRestrictedCode()
    {
        await StartAsync();

        var response = await Client.PostAsync("/blocked", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ReadErrorCodeAsync(response)).ShouldBe(DemoErrorCodes.Restricted);
    }

    [Fact]
    public async Task ConfiguredBlockedPathShouldReturn403ForWritesOnly()
    {
        _options.DemoBlockedPaths = ["/api/v1/secret"];
        await StartAsync();

        var write = await Client.PostAsync("/api/v1/secret/thing", content: null, TestContext.Current.CancellationToken);
        var read = await Client.GetAsync("/api/v1/secret/thing", TestContext.Current.CancellationToken);

        write.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SignInShouldCreateSandboxAndRememberItInCookie()
    {
        await StartAsync();

        var response = await Client.PostAsync("/login", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string sandboxId = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Manager.TryGet(sandboxId, out _).ShouldBeTrue();
        string cookie = response.Headers.GetValues("Set-Cookie").Single();
        cookie.ShouldContain($"{DemoModeMiddleware.SandboxCookieName}={sandboxId}");
        cookie.ShouldContain("httponly", Case.Insensitive);
    }

    [Fact]
    public async Task SignInWithCookieOfLiveSandboxShouldReuseIt()
    {
        await StartAsync();
        var first = await Client.PostAsync("/login", content: null, TestContext.Current.CancellationToken);
        string sandboxId = await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/login");
        request.Headers.Add("Cookie", $"{DemoModeMiddleware.SandboxCookieName}={sandboxId}");
        var second = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        (await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe(sandboxId);
        Manager.Count.ShouldBe(1);
    }

    [Fact]
    public async Task FailedSignInShouldNotLeaveASandboxBehind()
    {
        await StartAsync();

        var rejected = await Client.PostAsync("/login?fail=status", content: null, TestContext.Current.CancellationToken);
        await Should.ThrowAsync<InvalidOperationException>(
            () => Client.PostAsync("/login?fail=throw", content: null, TestContext.Current.CancellationToken));

        rejected.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        rejected.Headers.Contains("Set-Cookie").ShouldBeFalse();
        Manager.Count.ShouldBe(0);
        _database.Databases.ShouldBeEmpty();
    }

    [Fact]
    public async Task FailedSignInInReusedSandboxShouldKeepItButForgetTheCookie()
    {
        await StartAsync();
        var first = await Client.PostAsync("/login", content: null, TestContext.Current.CancellationToken);
        string sandboxId = await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/login?fail=status");
        request.Headers.Add("Cookie", $"{DemoModeMiddleware.SandboxCookieName}={sandboxId}");
        var second = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        second.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        second.Headers.GetValues("Set-Cookie").Single().ShouldStartWith($"{DemoModeMiddleware.SandboxCookieName}=;");
        Manager.TryGet(sandboxId, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task SignInWhenAddressStartedTooManySessionsShouldReturn429()
    {
        _options.Sandbox.NewSandboxesPerAddress = 1;
        await StartAsync();
        await Client.PostAsync("/login", content: null, TestContext.Current.CancellationToken);

        var response = await Client.PostAsync("/login", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await ReadErrorCodeAsync(response)).ShouldBe(DemoErrorCodes.TooManySessions);
        response.Headers.RetryAfter.ShouldNotBeNull();
        Manager.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SignInWhenDemoIsFullShouldReturn503()
    {
        _options.Sandbox.MaxSandboxes = 1;
        await StartAsync();
        await Client.PostAsync("/login", content: null, TestContext.Current.CancellationToken);

        var response = await Client.PostAsync("/login", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await ReadErrorCodeAsync(response)).ShouldBe(DemoErrorCodes.CapacityReached);
        response.Headers.RetryAfter.ShouldNotBeNull();
    }

    [Fact]
    public async Task AuthenticatedRequestShouldRunInTheSandboxNamedInItsToken()
    {
        await StartAsync();
        var sandbox = await Manager.CreateAsync(TestContext.Current.CancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/open");
        request.Headers.Add(SandboxHeader, sandbox.Id);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe(sandbox.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0123456789abcdef0123456789abcdef")]
    public async Task AuthenticatedRequestWithoutLiveSandboxShouldReturn401SessionExpired(string? sandboxId)
    {
        await StartAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/open");
        request.Headers.Add(UserHeader, "1");

        if (sandboxId is not null)
        {
            request.Headers.Add(SandboxHeader, sandboxId);
        }

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ReadErrorCodeAsync(response)).ShouldBe(DemoErrorCodes.SessionExpired);
    }

    [Fact]
    public async Task AuthenticatedRequestAfterSandboxExpiredShouldReturn401SessionExpired()
    {
        await StartAsync();
        var sandbox = await Manager.CreateAsync(TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromMinutes(_options.Sandbox.IdleTimeoutMinutes + 1));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/open");
        request.Headers.Add(SandboxHeader, sandbox.Id);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ReadErrorCodeAsync(response)).ShouldBe(DemoErrorCodes.SessionExpired);
    }

    [Fact]
    public async Task RefreshShouldRunInTheSandboxNamedInTheAccessTokenOfTheBody()
    {
        await StartAsync();
        var sandbox = await Manager.CreateAsync(TestContext.Current.CancellationToken);
        _tokenService.GetPrincipalFromExpiredToken("expired-token").Returns(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(DemoClaimTypes.SandboxId, sandbox.Id)])));

        var response = await Client.PostAsJsonAsync(
            "/refresh",
            new { accessToken = "expired-token", refreshToken = "r" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe($"{sandbox.Id}|expired-token");
    }

    [Theory]
    [InlineData(/*lang=json,strict*/ "{\"accessToken\":\"unknown\",\"refreshToken\":\"r\"}")]
    [InlineData(/*lang=json,strict*/ "{\"refreshToken\":\"r\"}")]
    [InlineData("not json")]
    public async Task RefreshWithoutLiveSandboxShouldReturn401SessionExpired(string body)
    {
        await StartAsync();

        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/refresh", content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ReadErrorCodeAsync(response)).ShouldBe(DemoErrorCodes.SessionExpired);
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return document.RootElement.GetProperty("errorCode").GetString();
    }

    private async Task StartAsync()
    {
        _manager = new DemoSandboxManager(
            _database,
            MsOptions.Create(_options),
            MsOptions.Create(new FileStorageOptions { UploadDirectory = Path.Combine(Path.GetTempPath(), "crm-demo-mw-tests"), BaseUrl = "http://localhost" }),
            _time,
            NullLogger<DemoSandboxManager>.Instance);

        _host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton(MsOptions.Create(_options));
                    services.AddSingleton(_manager);
                    services.AddSingleton(_tokenService);
                })
                .Configure(app =>
                {
                    app.UseRouting();

                    // Stands in for JWT authentication.
                    app.Use(async (context, next) =>
                    {
                        var claims = new List<Claim>();

                        if (context.Request.Headers.TryGetValue(SandboxHeader, out var sandboxId))
                        {
                            claims.Add(new Claim(DemoClaimTypes.SandboxId, sandboxId.ToString()));
                        }

                        if (claims.Count > 0 || context.Request.Headers.ContainsKey(UserHeader))
                        {
                            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
                        }

                        await next(context);
                    });

                    app.UseMiddleware<DemoModeMiddleware>();

                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/open", CurrentSandboxId);
                        endpoints.MapGet("/api/v1/secret/thing", CurrentSandboxId);
                        endpoints.MapPost("/api/v1/secret/thing", CurrentSandboxId);
                        endpoints.MapPost("/blocked", CurrentSandboxId).WithMetadata(new DemoBlockedAttribute());

                        endpoints.MapPost("/login", (HttpContext context) => context.Request.Query["fail"].ToString() switch
                        {
                            "status" => Results.Unauthorized(),
                            "throw" => throw new InvalidOperationException("bad credentials"),
                            _ => Results.Text(CurrentSandboxId(context)),
                        }).WithMetadata(new DemoSessionEndpointAttribute(DemoSessionEndpointKind.SignIn));

                        endpoints.MapPost("/refresh", async (HttpContext context) =>
                        {
                            // The endpoint must still be able to read the body the middleware looked into.
                            using var document = await JsonDocument.ParseAsync(context.Request.Body);
                            return $"{CurrentSandboxId(context)}|{document.RootElement.GetProperty("accessToken").GetString()}";
                        }).WithMetadata(new DemoSessionEndpointAttribute(DemoSessionEndpointKind.Refresh));
                    });
                }))
            .StartAsync(TestContext.Current.CancellationToken);

        _client = _host.GetTestClient();

        static string CurrentSandboxId(HttpContext context) => DemoSandboxAccessor.GetSandbox(context)?.Id ?? "none";
    }
}
