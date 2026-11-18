using System.Net;
using System.Text.Json;
using Crm.Api.DemoMode;
using Crm.Api.Middleware;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Crm.Api.Tests.DemoMode;

/// <summary>
/// Unit and pipeline tests for <see cref="DemoModeExtensions"/> verifying startup-only registration
/// (zero DI/middleware footprint when disabled, full decorator/middleware wiring when enabled),
/// options normalization, excluded-path replacement, and startup validation.
/// </summary>
public class DemoModeExtensionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public async Task AddAndUseDemoModeWhenDisabledOrMissingShouldRegisterNothingAndLeaveCoreServicesUndecorated(
        string? enabledValue)
    {
        var ct = TestContext.Current.CancellationToken;
        var overrides = new Dictionary<string, string?>();
        if (enabledValue is not null)
        {
            overrides[$"{DemoModeOptions.Position}:{nameof(DemoModeOptions.Enabled)}"] = enabledValue;
        }

        var innerMediator = Substitute.For<IMediator>();
        var innerUow = Substitute.For<IUnitOfWork>();
        var innerTracker = Substitute.For<IFileTransactionTracker>();
        var innerStorage = Substitute.For<IFileStorageService>();
        var innerCache = Substitute.For<IRolePermissionsCache>();
        var innerRefreshRepo = Substitute.For<IUserRefreshTokenRepository>();

        await using var app = await CreateAppAsync(
            overrides,
            services =>
            {
                services.AddScoped(_ => innerMediator);
                services.AddScoped<ISender>(_ => innerMediator);
                services.AddScoped<IPublisher>(_ => innerMediator);
                services.AddScoped(_ => innerUow);
                services.AddScoped(_ => innerTracker);
                services.AddScoped(_ => innerStorage);
                services.AddScoped(_ => innerRefreshRepo);
                services.AddSingleton(innerCache);
            },
            ct);

        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetService<IDemoModeState>().ShouldBeNull();
        sp.GetService<IDemoExecutionContext>().ShouldBeNull();
        sp.GetRequiredService<IMediator>().ShouldBeSameAs(innerMediator);
        sp.GetRequiredService<ISender>().ShouldBeSameAs(innerMediator);
        sp.GetRequiredService<IPublisher>().ShouldBeSameAs(innerMediator);
        sp.GetRequiredService<IUnitOfWork>().ShouldBeSameAs(innerUow);
        sp.GetRequiredService<IFileTransactionTracker>().ShouldBeSameAs(innerTracker);
        sp.GetRequiredService<IFileStorageService>().ShouldBeSameAs(innerStorage);
        sp.GetRequiredService<IUserRefreshTokenRepository>().ShouldBeSameAs(innerRefreshRepo);
        sp.GetRequiredService<IRolePermissionsCache>().ShouldBeSameAs(innerCache);

        using var client = app.GetTestClient();
        var blockedEndpointResp = await client.PostAsync("/api/v1/users/me/password", null, ct);
        blockedEndpointResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AddAndUseDemoModeWhenEnabledShouldDecorateServicesReplaceExcludedPathsAndReturnProblemJsonOnBlock()
    {
        var ct = TestContext.Current.CancellationToken;
        var overrides = new Dictionary<string, string?>
        {
            [$"{DemoModeOptions.Position}:{nameof(DemoModeOptions.Enabled)}"] = "true",
            [$"{DemoModeOptions.Position}:{nameof(DemoModeOptions.ExcludedPaths)}:0"] = "  api/v1/custom-excluded/  ",
            [$"{DemoModeOptions.Position}:{nameof(DemoModeOptions.DemoBlockedPaths)}:0"] = "api/v1/custom-blocked",
        };

        await using var app = await CreateAppAsync(
            overrides,
            services =>
            {
                var mediator = Substitute.For<IMediator>();
                services.AddScoped(_ => mediator);
                services.AddScoped<ISender>(_ => mediator);
                services.AddScoped<IPublisher>(_ => mediator);
                services.AddScoped(_ => Substitute.For<IUnitOfWork>());
                services.AddScoped(_ => Substitute.For<IFileTransactionTracker>());
                services.AddScoped(_ => Substitute.For<IFileStorageService>());
                services.AddScoped(_ => Substitute.For<IUserRefreshTokenRepository>());
                services.AddScoped(_ => Substitute.For<IImageValidator>());
                services.AddSingleton(Substitute.For<IRolePermissionsCache>());
            },
            ct);

        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetRequiredService<IDemoModeState>().IsActive.ShouldBeTrue();
        var resolvedMediator = sp.GetRequiredService<IMediator>();
        resolvedMediator.ShouldBeOfType<DemoMediator>();
        sp.GetRequiredService<ISender>().ShouldBeSameAs(resolvedMediator);
        sp.GetRequiredService<IPublisher>().ShouldBeSameAs(resolvedMediator);
        sp.GetRequiredService<IUnitOfWork>().ShouldBeOfType<DemoUnitOfWork>();
        sp.GetRequiredService<IFileTransactionTracker>().ShouldBeOfType<DemoFileTransactionTracker>();
        sp.GetRequiredService<IFileStorageService>().ShouldBeOfType<DemoFileStorageService>();
        sp.GetRequiredService<IUserRefreshTokenRepository>().ShouldBeOfType<DemoUserRefreshTokenRepository>();
        sp.GetRequiredService<IRolePermissionsCache>().ShouldBeOfType<DemoRolePermissionsCache>();

        var options = sp.GetRequiredService<IOptions<DemoModeOptions>>().Value;
        options.ExcludedPaths.ShouldBe(["/api/v1/custom-excluded"]);
        options.DemoBlockedPaths.ShouldBe(["/api/v1/custom-blocked"]);

        using var client = app.GetTestClient();

        var excludedResp = await client.PostAsync("/api/v1/custom-excluded", null, ct);
        excludedResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var attrBlockedResp = await client.PostAsync("/api/v1/users/me/password", null, ct);
        attrBlockedResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        attrBlockedResp.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problemJson = await JsonDocument.ParseAsync(await attrBlockedResp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        problemJson.RootElement.GetProperty("errorCode").GetString().ShouldBe(DemoModeMiddleware.RestrictedErrorCode);

        var pathBlockedResp = await client.PostAsync("/api/v1/custom-blocked", null, ct);
        pathBlockedResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        pathBlockedResp.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var regularWriteResp = await client.PostAsync("/api/v1/projects", null, ct);
        regularWriteResp.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("/api/v1/auth/login?bad=1")]
    [InlineData("/api/v1/auth/login#frag")]
    [InlineData("/")]
    [InlineData("/api")]
    [InlineData("/api/v1")]
    public async Task AddDemoModeWhenEnabledAndPathIsInvalidOrTooBroadShouldFailValidateOnStart(string invalidPath)
    {
        var ct = TestContext.Current.CancellationToken;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{DemoModeOptions.Position}:{nameof(DemoModeOptions.Enabled)}"] = "true",
            [$"{DemoModeOptions.Position}:{nameof(DemoModeOptions.ExcludedPaths)}:0"] = invalidPath,
        });

        builder.Services.AddDemoMode(builder.Configuration);

        await using var app = builder.Build();

        await Should.ThrowAsync<OptionsValidationException>(() => app.StartAsync(ct));
    }

    private static async Task<WebApplication> CreateAppAsync(
        IDictionary<string, string?> settings,
        Action<IServiceCollection> configureServices,
        CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(settings);

        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();
        configureServices(builder.Services);
        builder.Services.AddDemoMode(builder.Configuration);

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseDemoMode();

        app.MapPost("/api/v1/users/me/password", () => Results.NoContent())
            .WithMetadata(new DemoBlockedAttribute());
        app.MapPost("/api/v1/projects", () => Results.Created("/api/v1/projects", new { id = Guid.NewGuid() }));
        app.MapPost("/api/v1/custom-excluded", () => Results.Ok());
        app.MapPost("/api/v1/custom-blocked", () => Results.Ok());

        await app.StartAsync(cancellationToken);
        return app;
    }
}
