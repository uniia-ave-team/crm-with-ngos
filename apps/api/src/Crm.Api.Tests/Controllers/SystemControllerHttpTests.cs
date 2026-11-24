using System.Net;
using System.Text.Json;
using Crm.Api.Controllers.V1;
using Crm.Api.Extensions;
using Crm.Api.ModelBinding;
using Crm.Api.Serialization;
using Crm.Application.Dtos.System;
using Crm.Application.Features.System.Queries;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Crm.Api.Tests.Controllers;

/// <summary>
/// HTTP pipeline tests for <see cref="SystemController"/> verifying anonymous status response shape,
/// version serialization, and maintenance notice UTC serialization.
/// </summary>
public class SystemControllerHttpTests
{
    [Fact]
    public async Task GetSystemStatusShouldReturnVersionAndActiveNoticeAnonymously()
    {
        var ct = TestContext.Current.CancellationToken;
        var expectedNotice = new MaintenanceNoticeDto(
            "Planned upgrade",
            new DateTime(2026, 11, 20, 22, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 23, 0, 0, DateTimeKind.Utc));

        var mediator = new StubSystemMediator(new SystemStatusDto(
            HasAdmin: true,
            HasNgo: true,
            IsSetupComplete: true,
            Version: "main-3f2c1ab",
            Notice: expectedNotice));

        await using var app = await CreateSystemAppAsync(mediator, ct);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/api/v1/system/status", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        root.GetProperty("hasAdmin").GetBoolean().ShouldBeTrue();
        root.GetProperty("hasNgo").GetBoolean().ShouldBeTrue();
        root.GetProperty("isSetupComplete").GetBoolean().ShouldBeTrue();
        root.GetProperty("version").GetString().ShouldBe("main-3f2c1ab");

        var notice = root.GetProperty("notice");
        notice.GetProperty("message").GetString().ShouldBe("Planned upgrade");
        notice.GetProperty("startsAt").GetString().ShouldBe("2026-11-20T22:00:00Z");
        notice.GetProperty("endsAt").GetString().ShouldBe("2026-11-20T23:00:00Z");
    }

    [Fact]
    public async Task GetSystemStatusShouldSerializeNullNoticeWhenNoneIsActive()
    {
        var ct = TestContext.Current.CancellationToken;
        var mediator = new StubSystemMediator(new SystemStatusDto(
            HasAdmin: false,
            HasNgo: false,
            IsSetupComplete: false,
            Version: "dev",
            Notice: null));

        await using var app = await CreateSystemAppAsync(mediator, ct);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/api/v1/system/status", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        root.GetProperty("version").GetString().ShouldBe("dev");
        root.GetProperty("notice").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    private static async Task<WebApplication> CreateSystemAppAsync(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);
        builder.Services.AddControllers(options =>
            {
                options.ModelBinderProviders.Insert(0, new UtcDateTimeModelBinderProvider());
            })
            .AddApplicationPart(typeof(SystemController).Assembly)
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
            });
        builder.Services.AddCustomApiVersioning();
        builder.Services.AddSingleton(mediator);

        var app = builder.Build();
        app.MapControllers();

        await app.StartAsync(cancellationToken);
        return app;
    }

    private sealed class StubSystemMediator(SystemStatusDto statusDto) : IMediator
    {
        public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            return request is GetSystemSetupStatusQuery && statusDto is TResponse typed
                ? ValueTask.FromResult(typed)
                : throw new InvalidOperationException($"Unexpected request {request.GetType().Name}");
        }

        public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
        {
            return query is GetSystemSetupStatusQuery && statusDto is TResponse typed
                ? ValueTask.FromResult(typed)
                : throw new InvalidOperationException($"Unexpected query {query.GetType().Name}");
        }

        public ValueTask<object?> Send(object message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamQuery<TResponse> query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamCommand<TResponse> command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => throw new NotSupportedException();

        public ValueTask Publish(object notification, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
