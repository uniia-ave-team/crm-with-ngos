using System.Text.Json;
using Crm.Api.DemoMode;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Crm.Api.Tests.DemoMode;

/// <summary>
/// Unit tests for <see cref="DemoModeMiddleware"/> verifying pass-through for normal writes and reads,
/// blocking of explicitly configured sensitive endpoints with <c>403 Forbidden</c> ProblemDetails,
/// excluded paths, and path prefix normalization.
/// </summary>
public class DemoModeMiddlewareTests
{
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task InvokeAsyncWhenDemoModeEnabledAndRegularWriteEndpointShouldPassThroughToNext(string method)
    {
        bool nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            ctx.Response.StatusCode = StatusCodes.Status201Created;
            return Task.CompletedTask;
        };

        var middleware = new DemoModeMiddleware(
            next,
            MsOptions.Create(new DemoModeOptions { Enabled = true }),
            Substitute.For<ILogger<DemoModeMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = "/api/v1/projects";

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status201Created);
    }

    [Theory]
    [InlineData("POST", "/api/v1/users/me/password")]
    [InlineData("POST", "/api/v1/users/reset-password")]
    [InlineData("POST", "/api/v1/users")]
    [InlineData("PUT", "/api/v1/users/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", "/api/v1/users/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/v1/roles")]
    [InlineData("PUT", "/api/v1/roles/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", "/api/v1/roles/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/v1/ngo")]
    [InlineData("PUT", "/api/v1/ngo")]
    public async Task InvokeAsyncWhenDemoModeEnabledAndBlockedPathWriteShouldReturn403ProblemDetails(
        string method,
        string path)
    {
        bool nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new DemoModeMiddleware(
            next,
            MsOptions.Create(new DemoModeOptions
            {
                Enabled = true,
                DemoBlockedPaths = ["/api/v1/users", "/api/v1/roles", "/api/v1/ngo"],
            }),
            Substitute.For<ILogger<DemoModeMiddleware>>());

        var context = CreateHttpContext(method, path);

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeFalse();
        await AssertBlocked403Async(context);
    }

    [Fact]
    public async Task InvokeAsyncWhenEndpointHasDemoBlockedAttributeShouldReturn403OnWrite()
    {
        bool nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new DemoModeMiddleware(
            next,
            MsOptions.Create(new DemoModeOptions
            {
                Enabled = true,
                DemoBlockedPaths = [],
            }),
            Substitute.For<ILogger<DemoModeMiddleware>>());

        var context = CreateHttpContext(HttpMethods.Post, "/api/v1/custom-sensitive-action");
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new DemoBlockedAttribute()),
            "SensitiveEndpoint"));

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeFalse();
        await AssertBlocked403Async(context);
    }

    [Theory]
    [InlineData("/api/v1/users")]
    [InlineData("/api/v1/roles")]
    [InlineData("/api/v1/ngo")]
    [InlineData("/api/v1/projects")]
    public async Task InvokeAsyncWhenDemoModeEnabledAndGetMethodShouldInvokeNextEvenOnBlockedPaths(string path)
    {
        bool nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };

        var middleware = new DemoModeMiddleware(
            next,
            MsOptions.Create(new DemoModeOptions { Enabled = true }),
            Substitute.For<ILogger<DemoModeMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new DemoBlockedAttribute()),
            "ReadEndpoint"));

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeTrue();
    }

    [Theory]
    [InlineData("/api/v1/auth/login")]
    [InlineData("/api/v1/auth/refresh-token")]
    [InlineData("/api/v1/auth/logout")]
    public async Task InvokeAsyncWhenAuthPathIsExcludedByDefaultShouldInvokeNextInDemoMode(string path)
    {
        bool nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };

        var middleware = new DemoModeMiddleware(
            next,
            MsOptions.Create(new DemoModeOptions { Enabled = true }),
            Substitute.For<ILogger<DemoModeMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = path;

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsyncWhenConfiguredPathsLackLeadingSlashShouldNormalizeWithoutThrowingArgumentException()
    {
        bool nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new DemoModeMiddleware(
            next,
            MsOptions.Create(new DemoModeOptions
            {
                Enabled = true,
                ExcludedPaths = ["  api/v1/auth/login  ", "   "],
                DemoBlockedPaths = ["api/v1/custom-blocked/"],
            }),
            Substitute.For<ILogger<DemoModeMiddleware>>());

        var excludedCtx = CreateHttpContext(HttpMethods.Post, "/api/v1/auth/login");
        await middleware.InvokeAsync(excludedCtx);
        nextCalled.ShouldBeTrue();

        nextCalled = false;
        var blockedCtx = CreateHttpContext(HttpMethods.Post, "/api/v1/custom-blocked/item");
        await middleware.InvokeAsync(blockedCtx);
        nextCalled.ShouldBeFalse();
        await AssertBlocked403Async(blockedCtx);
    }

    private static DefaultHttpContext CreateHttpContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task AssertBlocked403Async(DefaultHttpContext context)
    {
        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        context.Response.ContentType.ShouldNotBeNull();
        context.Response.ContentType.ShouldStartWith("application/problem+json");
        context.Response.Body.Position = 0;
        var json = await JsonDocument.ParseAsync(context.Response.Body);
        json.RootElement.GetProperty("status").GetInt32().ShouldBe(403);
        json.RootElement.GetProperty("errorCode").GetString().ShouldBe(DemoModeMiddleware.RestrictedErrorCode);
    }
}
