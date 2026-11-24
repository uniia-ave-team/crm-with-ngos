using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Crm.Api.Controllers.V1;
using Crm.Api.Extensions;
using Crm.Api.Middleware;
using Crm.Api.ModelBinding;
using Crm.Api.Serialization;
using Crm.Application.Dtos.Common;
using Crm.Application.Dtos.Tasks;
using Crm.Application.Features.Projects.Commands;
using Crm.Application.Features.Tasks.Commands;
using Crm.Application.Features.Tasks.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Mediator;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Crm.Api.Tests.Controllers;

/// <summary>
/// In-memory ASP.NET Core HTTP pipeline tests verifying routing, <see cref="AccessRight"/> authorization,
/// UTC <see cref="DateTime"/> JSON/query binding, required PUT fields, database conflict exception mapping,
/// and multipart attachment upload/download security headers.
/// </summary>
public class ProjectsAndTasksHttpPipelineTests
{
    [Fact]
    public async Task ProjectsAndTasksControllersShouldEnforceAccessRightsBindUtcDatesAndSetDownloadSecurityHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var createdProjectId = Guid.NewGuid();

        var mediator = new StubMediator
        {
            Handler = request => request switch
            {
                CreateProjectCommand cmd when cmd.Title == "Bad" => throw new EmptyFieldSchemaException(),
                CreateProjectCommand cmd when cmd.Title == "UniqueConflict" => throw new ConflictException(
                    "A resource with the same unique value already exists.",
                    ConflictKind.UniqueViolation,
                    new InvalidOperationException("SQL constraint leak: duplicate key")),
                CreateProjectCommand cmd when cmd.Title == "FkConflict" => throw new ConflictException(
                    "The operation could not be completed because a referenced or dependent resource does not exist or is still in use.",
                    ConflictKind.ReferenceViolation,
                    new InvalidOperationException("fk violation detail")),
                CreateProjectCommand cmd when cmd.Title == "ConcurrencyConflict" => throw new ConcurrencyConflictException(
                    "The resource was modified or deleted concurrently.",
                    new InvalidOperationException("concurrency leak detail")),
                CreateProjectCommand cmd when cmd.Title == "QuotaExceeded" => throw new StorageQuotaExceededException(
                    "File storage quota of 100 bytes exceeded."),
                CreateProjectCommand => createdProjectId,
                GetProjectTasksQuery => new PagedResult<TaskSummaryDto>([], 0, 1, 10),
                GetTaskCommentsQuery q => new PagedResult<TaskCommentDto>([], 0, q.PageNumber, q.PageSize),
                CreateTaskCommand => taskId,
                UpdateTaskCommand => Unit.Value,
                UploadTaskAttachmentCommand => new TaskAttachmentDto(
                    attachmentId,
                    taskId,
                    Guid.NewGuid(),
                    "plan.txt",
                    "text/plain",
                    11,
                    "http://localhost/api/v1/projects/p/tasks/t/attachments/a",
                    DateTime.UtcNow),
                GetTaskAttachmentFileQuery => new FileDto(new MemoryStream(Encoding.UTF8.GetBytes("hello world")), "plan.txt"),
                _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}"),
            },
        };

        bool allowAccess = true;
        var permissionService = Substitute.For<IPermissionService>();
        permissionService.HasAccessAsync(Arg.Any<List<Guid>>(), Arg.Any<AccessRight>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(allowAccess));

        await using var app = await CreateTestAppAsync(mediator, permissionService, ct);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");

        // Removed YAGNI /claims endpoint returns 404
        var removedClaimsResp = await client.GetAsync("/api/v1/projects/claims", ct);
        removedClaimsResp.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Missing global AccessRight returns 403 Forbidden (B-16)
        allowAccess = false;
        var deniedResp = await client.GetAsync("/api/v1/tasks?filter=mine", ct);
        deniedResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        allowAccess = true;

        // Domain validation exception mapped to 400 Bad Request
        var badProjectResp = await client.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectCommand("Bad", null, ProjectVisibility.Private, DateTime.UtcNow),
            ct);
        badProjectResp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Domain ConflictException / ConcurrencyConflictException map to 409 with distinct messages and without leaking inner SQL details
        var uniqueConflictResp = await client.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectCommand("UniqueConflict", null, ProjectVisibility.Public, DateTime.UtcNow),
            ct);
        uniqueConflictResp.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var uniqueBody = await uniqueConflictResp.Content.ReadAsStringAsync(ct);
        uniqueBody.ShouldContain("A resource with the same unique value already exists.");
        uniqueBody.ShouldNotContain("SQL constraint leak");
        uniqueBody.ShouldNotContain("duplicate key");

        var fkConflictResp = await client.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectCommand("FkConflict", null, ProjectVisibility.Public, DateTime.UtcNow),
            ct);
        fkConflictResp.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var fkBody = await fkConflictResp.Content.ReadAsStringAsync(ct);
        fkBody.ShouldContain("referenced or dependent resource");
        fkBody.ShouldNotContain("fk violation detail");

        var concurrencyConflictResp = await client.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectCommand("ConcurrencyConflict", null, ProjectVisibility.Public, DateTime.UtcNow),
            ct);
        concurrencyConflictResp.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var concurrencyBody = await concurrencyConflictResp.Content.ReadAsStringAsync(ct);
        concurrencyBody.ShouldContain("modified or deleted concurrently");
        concurrencyBody.ShouldNotContain("concurrency leak detail");

        // StorageQuotaExceededException maps to 413 Payload Too Large with clear message (NOT 500)
        var quotaResp = await client.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectCommand("QuotaExceeded", null, ProjectVisibility.Public, DateTime.UtcNow),
            ct);
        quotaResp.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        var quotaBody = await quotaResp.Content.ReadAsStringAsync(ct);
        quotaBody.ShouldContain("File storage quota of 100 bytes exceeded.");

        // Paginated GET /comments binds pageNumber and pageSize
        var commentsPageResp = await client.GetAsync(
            $"/api/v1/projects/{projectId}/tasks/{taskId}/comments?pageNumber=2&pageSize=5",
            ct);
        commentsPageResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        mediator.SentRequests.OfType<GetTaskCommentsQuery>()
            .ShouldContain(q => q.ProjectId == projectId && q.TaskId == taskId && q.PageNumber == 2 && q.PageSize == 5);

        // DateTime without Z and with timezone offset normalize to UTC in JSON body (B-09)
        using var rawUtcBody = new StringContent(
            JsonSerializer.Serialize(new
            {
                title = "Dates",
                description = (string?)null,
                visibility = 2,
                startDate = "2026-11-20T00:00:00",
                endDate = "2026-11-20T02:00:00+02:00",
            }),
            Encoding.UTF8,
            "application/json");
        var dateBodyResp = await client.PostAsync("/api/v1/projects", rawUtcBody, ct);
        dateBodyResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dateCmd = mediator.SentRequests.OfType<CreateProjectCommand>().Last();
        dateCmd.StartDate.Kind.ShouldBe(DateTimeKind.Utc);
        dateCmd.StartDate.ShouldBe(new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc));
        dateCmd.EndDate.ShouldNotBeNull();
        dateCmd.EndDate.Value.Kind.ShouldBe(DateTimeKind.Utc);
        dateCmd.EndDate.Value.ShouldBe(new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc));

        // PUT /tasks/{id} omitting required status/priority returns 400 Bad Request instead of silently resetting (B-11)
        using var missingStatusPayload = new StringContent(
            $$"""{"topicId":"{{Guid.NewGuid()}}","title":"Partial PUT","description":null,"startDate":"2026-11-20T00:00:00Z"}""",
            Encoding.UTF8,
            "application/json");
        var partialPutResp = await client.PutAsync($"/api/v1/projects/{projectId}/tasks/{taskId}", missingStatusPayload, ct);
        partialPutResp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // POST /tasks omitting optional tagIds / assigneeIds succeeds (201 Created)
        using var createTaskWithoutTagsPayload = new StringContent(
            $$"""{"topicId":"{{Guid.NewGuid()}}","title":"Task Without Tags","description":null,"startDate":"2026-11-20T00:00:00","status":1,"priority":2,"endDate":"2026-11-20T02:00:00+02:00","assigneeIds":["{{Guid.NewGuid()}}"]}""",
            Encoding.UTF8,
            "application/json");
        var createTaskWithoutTagsResp = await client.PostAsync($"/api/v1/projects/{projectId}/tasks", createTaskWithoutTagsPayload, ct);
        createTaskWithoutTagsResp.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Global GET /api/v1/tasks binds ProjectId=null even when spoofed, and normalizes query dates to UTC (B-08, B-09)
        var spoofedProjectId = Guid.NewGuid();
        var globalResp = await client.GetAsync(
            $"/api/v1/tasks?filter=mine&projectId={spoofedProjectId}&startDateFrom=2026-11-20T00:00:00&startDateTo=2026-11-20T02:00:00%2B02:00",
            ct);
        globalResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var globalQuery = mediator.SentRequests.OfType<GetProjectTasksQuery>().Last();
        globalQuery.ProjectId.ShouldBeNull();
        globalQuery.Filter.ShouldBe("mine");
        globalQuery.StartDateFrom.ShouldBe(new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc));
        globalQuery.StartDateFrom!.Value.Kind.ShouldBe(DateTimeKind.Utc);
        globalQuery.StartDateTo.ShouldBe(new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc));
        globalQuery.StartDateTo!.Value.Kind.ShouldBe(DateTimeKind.Utc);

        // Project-scoped GET /api/v1/projects/{projectId}/tasks preserves route projectId
        var taggedId1 = Guid.NewGuid();
        var taggedId2 = Guid.NewGuid();
        var mineResp = await client.GetAsync(
            $"/api/v1/projects/{projectId}/tasks?filter=mine&tagIds={taggedId1}&tagIds={taggedId2}&projectId={spoofedProjectId}",
            ct);
        mineResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        mediator.SentRequests.OfType<GetProjectTasksQuery>()
            .ShouldContain(q =>
                q.ProjectId == projectId &&
                q.Filter == "mine" &&
                q.TagIds != null &&
                q.TagIds.SequenceEqual(new[] { taggedId1, taggedId2 }));

        // Multipart attachment upload and download with X-Content-Type-Options: nosniff & Content-Disposition: attachment (B-14)
        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("hello world"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(fileContent, "file", "plan.txt");

        var uploadResp = await client.PostAsync($"/api/v1/projects/{projectId}/tasks/{taskId}/attachments", form, ct);
        uploadResp.StatusCode.ShouldBe(HttpStatusCode.Created);

        var downloadResp = await client.GetAsync($"/api/v1/projects/{projectId}/tasks/{taskId}/attachments/{attachmentId}", ct);
        downloadResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        downloadResp.Headers.TryGetValues("X-Content-Type-Options", out var nosniffValues).ShouldBeTrue();
        nosniffValues.ShouldContain("nosniff");
        downloadResp.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("attachment");
        (await downloadResp.Content.ReadAsStringAsync(ct)).ShouldBe("hello world");
    }

    private static async Task<WebApplication> CreateTestAppAsync(
        IMediator mediator,
        IPermissionService permissionService,
        CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = "Production";
        builder.WebHost.UseTestServer();

        builder.Services.Configure<RouteOptions>(o => o.LowercaseUrls = true);
        builder.Services.AddControllers(options =>
        {
            options.ModelBinderProviders.Insert(0, new UtcDateTimeModelBinderProvider());
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
        })
        .AddApplicationPart(typeof(ProjectsController).Assembly);

        builder.Services.AddCustomApiVersioning();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();

        builder.Services.AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddCustomAuthorization();

        var roleId = Guid.NewGuid();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.GetUserId().Returns(Guid.NewGuid());
        currentUserService.GetRoleIds().Returns([roleId]);
        builder.Services.AddScoped(_ => currentUserService);
        builder.Services.AddScoped(_ => permissionService);
        builder.Services.AddSingleton(mediator);

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        await app.StartAsync(cancellationToken);
        return app;
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
                Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private sealed class StubMediator : IMediator
    {
        public List<object> SentRequests { get; } = [];

        public Func<object, object?> Handler { get; init; } = _ => null;

        public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            SentRequests.Add(request);
            var result = Handler(request);
            return ValueTask.FromResult((TResponse)result!);
        }

        public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
        {
            SentRequests.Add(command);
            var result = Handler(command);
            return ValueTask.FromResult((TResponse)result!);
        }

        public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
        {
            SentRequests.Add(query);
            var result = Handler(query);
            return ValueTask.FromResult((TResponse)result!);
        }

        public ValueTask<object?> Send(object message, CancellationToken cancellationToken = default)
        {
            SentRequests.Add(message);
            return ValueTask.FromResult(Handler(message));
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamCommand<TResponse> command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamQuery<TResponse> query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => ValueTask.CompletedTask;

        public ValueTask Publish(object notification, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }
}
