using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Crm.Api.Controllers.V1;
using Crm.Api.Extensions;
using Crm.Api.Security;
using Crm.Api.Serialization;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Crm.Api.Tests;

/// <summary>
/// API-level smoke and OpenAPI schema generation tests.
/// </summary>
public class ApiTests
{
    /// <summary>
    /// Verifies that the full OpenAPI <c>v1</c> document generates cleanly for all API controllers
    /// with <see cref="UtcDateTimeJsonConverter"/> enabled (preventing runtime schema serialization regressions).
    /// </summary>
    [Fact]
    public async Task OpenApiV1DocumentShouldGenerateWithoutSerializationErrors()
    {
        var ct = TestContext.Current.CancellationToken;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
        });
        builder.WebHost.UseTestServer();

        builder.Services.Configure<RouteOptions>(options =>
        {
            options.LowercaseUrls = true;
        });

        builder.Services.AddControllers()
            .AddApplicationPart(typeof(ProjectsController).Assembly)
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
            });

        builder.Services.AddCustomApiVersioning();

        await using var app = builder.Build();
        app.MapOpenApi().WithDocumentPerVersion();
        await app.StartAsync(ct);

        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/openapi/v1.json", ct);
        string body = await response.Content.ReadAsStringAsync(ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"GET /openapi/v1.json failed: {body}");

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("openapi").GetString().ShouldNotBeNullOrWhiteSpace();
        doc.RootElement.GetProperty("paths").GetProperty("/api/v1/projects").ValueKind.ShouldBe(JsonValueKind.Object);
    }

    /// <summary>
    /// Verifies that <see cref="AccessRightAuthorizationHandler"/> logs the authenticated user's email or <c>sub</c>
    /// identifier on access denial instead of falling back to <c>'Unknown'</c> when <see cref="ClaimTypes.Name"/> is absent.
    /// </summary>
    [Fact]
    public async Task AccessRightAuthorizationHandlerShouldLogUserEmailOrSubInsteadOfUnknownOnAccessDenied()
    {
        var roleId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.GetRoleIds().Returns([roleId]);

        var permissionService = Substitute.For<IPermissionService>();
        permissionService.HasAccessAsync(Arg.Any<List<Guid>>(), AccessRight.DeleteUser, Arg.Any<CancellationToken>())
            .Returns(false);

        var recordedLogs = new List<string>();
        var logger = new CapturingLogger<AccessRightAuthorizationHandler>(recordedLogs);

        var handler = new AccessRightAuthorizationHandler(currentUserService, permissionService, logger);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(CustomClaimTypes.Sub, userId.ToString()),
            new Claim(ClaimTypes.Email, "member@ngo.org"),
            new Claim(CustomClaimTypes.RoleId, roleId.ToString()),
        ]));

        var requirement = new AccessRightRequirement(AccessRight.DeleteUser);
        var authContext = new AuthorizationHandlerContext([requirement], principal, resource: null);

        await handler.HandleAsync(authContext);

        authContext.HasSucceeded.ShouldBeFalse();
        recordedLogs.ShouldContain(m => m.Contains("member@ngo.org", StringComparison.Ordinal) && !m.Contains("'Unknown'", StringComparison.Ordinal));
    }

    private sealed class CapturingLogger<T>(List<string> messages) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => messages.Add(formatter(state, exception));
    }
}
