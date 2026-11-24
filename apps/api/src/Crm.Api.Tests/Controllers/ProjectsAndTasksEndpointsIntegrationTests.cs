using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.Projects;
using Crm.Application.Dtos.Roles;
using Crm.Application.Dtos.Tasks;
using Crm.Application.Dtos.Users;
using Crm.Application.Features.Ngos.Commands;
using Crm.Application.Features.Projects.Commands;
using Crm.Application.Features.Roles.Commands;
using Crm.Application.Features.Tasks.Commands;
using Crm.Application.Features.Users.Commands;
using Crm.Domain.Common;
using Crm.Domain.Constants.Validation;
using Crm.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Crm.Api.Tests.Controllers;

/// <summary>
/// End-to-end PostgreSQL HTTP integration tests for Projects, Project Members, Topics, Tags,
/// Tasks (Board, Global My Tasks, 5-status lifecycle, ExternalLinks/Metadata JSONB),
/// Comments, <c>TaskReadState</c> unread tracking, Task File Attachments, UTC DateTime normalization,
/// concurrency conflict handling, and global <see cref="AccessRight"/> authorization.
/// </summary>
/// <param name="fixture">The shared PostgreSQL Testcontainers fixture.</param>
public class ProjectsAndTasksEndpointsIntegrationTests(
    ProjectsAndTasksEndpointsIntegrationTests.ApiPostgresFixture fixture)
    : IClassFixture<ProjectsAndTasksEndpointsIntegrationTests.ApiPostgresFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task FullTaskTrackerWorkflowShouldEnforcePermissionsStatusesMetadataUnreadReadStateAndAttachments()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var databaseName = $"crm_workflow_{Guid.NewGuid():N}";
        using var factory = fixture.CreateFactory(databaseName);
        var (client1, user1Id, adminRoleId) = await SeedAdminAndNgoAsync(factory, "ivan@ngo.org", "Ivan", "Franko", ct);
        using var client1Scope = client1;
        var (client2, user2Id) = await InviteAndRegisterUserAsync(factory, client1, "olena@ngo.org", "Olena", "Kobylianska", adminRoleId, ct);
        using var client2Scope = client2;

        // 1. Validate invalid project visibility enum value -> 400
        var invalidProjectResp = await client1.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectCommand("Invalid", null, (ProjectVisibility)99, DateTime.UtcNow),
            ct);
        invalidProjectResp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // 2. Create Private project by User 1
        var createProjectResp = await client1.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectCommand("Grant Campaign", "Q4 campaign", ProjectVisibility.Private, DateTime.UtcNow),
            ct);
        createProjectResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var projectId = await createProjectResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        // B-17: Outsider gets 404 Not Found (NOT 403) on Private project so existence is not disclosed
        var hiddenGetResp = await client2.GetAsync($"/api/v1/projects/{projectId}", ct);
        hiddenGetResp.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var hiddenDeleteResp = await client2.DeleteAsync($"/api/v1/projects/{projectId}", ct);
        hiddenDeleteResp.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // 3. User 1 assigns User 2 with custom roleTitle and "View" claim only
        var assignMemberResp = await client1.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/members",
            new AssignProjectMemberCommand(user2Id, "SMM-менеджер", ProjectClaim.View),
            ct);
        assignMemberResp.StatusCode.ShouldBe(HttpStatusCode.Created);

        // User 2 can now view project (200), but cannot create topics (403)
        var allowedGetResp = await client2.GetAsync($"/api/v1/projects/{projectId}", ct);
        allowedGetResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var forbiddenTopicResp = await client2.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/topics",
            new CreateTopicCommand("SMM", 0),
            ct);
        forbiddenTopicResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Upgrade User 2 to "Edit" claim
        var updateMemberResp = await client1.PutAsJsonAsync(
            $"/api/v1/projects/{projectId}/members/{user2Id}",
            new UpdateProjectMemberCommand("Головний SMM-менеджер", ProjectClaim.Edit),
            ct);
        updateMemberResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 4. Create Topics (contiguous 0-based order) and Project-Scoped Tags (case-insensitive uniqueness B-10)
        var nonSequentialFirstTopicResp = await client2.PostAsJsonAsync($"/api/v1/projects/{projectId}/topics", new CreateTopicCommand("SMM", 1), ct);
        nonSequentialFirstTopicResp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var topicDevResp = await client1.PostAsJsonAsync($"/api/v1/projects/{projectId}/topics", new CreateTopicCommand("Розробка", 0), ct);
        topicDevResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var topicDevId = await topicDevResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        var topicSmmResp = await client2.PostAsJsonAsync($"/api/v1/projects/{projectId}/topics", new CreateTopicCommand("SMM", 1), ct);
        topicSmmResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var topicSmmId = await topicSmmResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        var duplicateOrderTopicResp = await client1.PostAsJsonAsync($"/api/v1/projects/{projectId}/topics", new CreateTopicCommand("Дубль позиції", 0), ct);
        duplicateOrderTopicResp.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var gapOrderTopicResp = await client1.PostAsJsonAsync($"/api/v1/projects/{projectId}/topics", new CreateTopicCommand("Пропуск позиції", 3), ct);
        gapOrderTopicResp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var tagResp = await client1.PostAsJsonAsync($"/api/v1/projects/{projectId}/tags", new CreateTagCommand("Urgent"), ct);
        tagResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var tagId = await tagResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        var duplicateCaseTagResp = await client1.PostAsJsonAsync($"/api/v1/projects/{projectId}/tags", new CreateTagCommand("  uRgEnT "), ct);
        duplicateCaseTagResp.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // 5. Create tasks with ExternalLinks & Metadata JSONB
        var taskStartDate = new DateTime(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);
        var taskForUser2Resp = await client1.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks",
            new CreateTaskCommand(
                topicSmmId,
                "Prepare SMM plan",
                [user2Id],
                [tagId],
                "## Markdown plan",
                taskStartDate,
                ProjectTaskStatus.InProgress,
                TaskPriority.High,
                EndDate: null,
                ExternalLinks: [new TaskExternalLinkDto("Brief", "https://example.org/brief")],
                Metadata: new JsonObject { ["channel"] = "instagram", ["campaignYear"] = 2026 }),
            ct);
        taskForUser2Resp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var taskForUser2Id = await taskForUser2Resp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        var taskForUser1Resp = await client1.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks",
            new CreateTaskCommand(
                topicDevId,
                "Build API",
                [user1Id],
                [],
                "Backend endpoints",
                new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc),
                ProjectTaskStatus.ToDo,
                TaskPriority.Critical,
                EndDate: null),
            ct);
        taskForUser1Resp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var taskForUser1Id = await taskForUser1Resp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        // 6. TaskReadState unread tracking (B-12): User 1 comments on BOTH tasks
        var c1Resp = await client1.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}/comments",
            new CreateTaskCommentCommand("Please review the draft"),
            ct);
        c1Resp.StatusCode.ShouldBe(HttpStatusCode.Created);

        var c2Resp = await client1.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks/{taskForUser1Id}/comments",
            new CreateTaskCommentCommand("Database schema ready"),
            ct);
        c2Resp.StatusCode.ShouldBe(HttpStatusCode.Created);

        // User 1's own comments are NOT unread for User 1
        var user1Mine = await client1.GetFromJsonAsync<PagedResult<TaskSummaryDto>>(
            $"/api/v1/projects/{projectId}/tasks?filter=mine",
            JsonOptions,
            ct);
        user1Mine.ShouldNotBeNull();
        user1Mine.Items.ShouldHaveSingleItem().UnreadCommentsCount.ShouldBe(0);

        // User 2 sees TaskSummaryDto with UnreadCommentsCount == 1
        var mineView = await client2.GetFromJsonAsync<PagedResult<TaskSummaryDto>>(
            $"/api/v1/projects/{projectId}/tasks?filter=mine",
            JsonOptions,
            ct);
        mineView.ShouldNotBeNull();
        var mineTask = mineView.Items.ShouldHaveSingleItem();
        mineTask.Id.ShouldBe(taskForUser2Id);
        mineTask.UnreadCommentsCount.ShouldBe(1);
        mineTask.AttachmentsCount.ShouldBe(0);
        mineTask.AssignedUsers.ShouldHaveSingleItem().Id.ShouldBe(user2Id);

        // User 2 marks comments as read -> TaskReadState row is deleted and unreadCommentsCount = 0
        var markReadResp = await client2.PostAsync(
            $"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}/comments/read",
            null,
            ct);
        markReadResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var mineAfterRead = await client2.GetFromJsonAsync<PagedResult<TaskSummaryDto>>(
            $"/api/v1/projects/{projectId}/tasks?filter=mine",
            JsonOptions,
            ct);
        mineAfterRead.ShouldNotBeNull();
        mineAfterRead.Items.Single().UnreadCommentsCount.ShouldBe(0);

        // Subsequent comment by User 1 recreates TaskReadState and makes unreadCommentsCount == 1 again
        var c3Resp = await client1.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}/comments",
            new CreateTaskCommentCommand("One more update"),
            ct);
        c3Resp.StatusCode.ShouldBe(HttpStatusCode.Created);

        var mineAfterNewComment = await client2.GetFromJsonAsync<PagedResult<TaskSummaryDto>>(
            $"/api/v1/projects/{projectId}/tasks?filter=mine",
            JsonOptions,
            ct);
        mineAfterNewComment.ShouldNotBeNull();
        mineAfterNewComment.Items.Single().UnreadCommentsCount.ShouldBe(1);

        // Second MarkCommentsAsRead deletes TaskReadState cleanly (and repeated calls are idempotent)
        var markReadAgainResp = await client2.PostAsync(
            $"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}/comments/read",
            null,
            ct);
        markReadAgainResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var markReadIdempotentResp = await client2.PostAsync(
            $"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}/comments/read",
            null,
            ct);
        markReadIdempotentResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 7. 5-status lifecycle + EndDateWasAuto + EndDate >= StartDate validation (B-06, B-07)
        // Done does NOT auto-populate EndDate
        var doneResp = await client2.PatchAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}/status",
            new UpdateTaskStatusCommand(ProjectTaskStatus.Done),
            ct);
        doneResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var doneTask = await client2.GetFromJsonAsync<TaskDto>(
            $"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}",
            JsonOptions,
            ct);
        doneTask.ShouldNotBeNull();
        doneTask.Status.ShouldBe(ProjectTaskStatus.Done);
        doneTask.EndDate.ShouldBeNull();
        doneTask.ExternalLinks.ShouldHaveSingleItem().Url.ShouldBe("https://example.org/brief");
        doneTask.Metadata["channel"]?.GetValue<string>().ShouldBe("instagram");

        // Invalid explicit EndDate < StartDate on PATCH returns 400
        var invalidEndDatePatch = await client2.PatchAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}/status",
            new UpdateTaskStatusCommand(ProjectTaskStatus.Completed, EndDate: taskStartDate.AddDays(-1)),
            ct);
        invalidEndDatePatch.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Completed with null EndDate auto-populates EndDate
        var completeResp = await client2.PatchAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}/status",
            new UpdateTaskStatusCommand(ProjectTaskStatus.Completed),
            ct);
        completeResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var completedTask = await client2.GetFromJsonAsync<TaskDto>(
            $"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}",
            JsonOptions,
            ct);
        completedTask.ShouldNotBeNull();
        completedTask.Status.ShouldBe(ProjectTaskStatus.Completed);
        completedTask.EndDate.ShouldNotBeNull();
        completedTask.EndDateWasAuto.ShouldBeTrue();

        // Editing an auto-completed task via PUT echoing the unchanged EndDate preserves EndDateWasAuto
        var editAutoCompletedResp = await client2.PutAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}",
            new UpdateTaskCommand(
                topicSmmId,
                "Prepare SMM plan (edited)",
                [user2Id],
                [tagId],
                "## Markdown plan",
                taskStartDate,
                ProjectTaskStatus.Completed,
                TaskPriority.High,
                EndDate: completedTask.EndDate),
            ct);
        editAutoCompletedResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // PUT moving StartDate after existing EndDate with null EndDate returns 400
        var invalidPutRangeResp = await client2.PutAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}",
            new UpdateTaskCommand(
                topicSmmId,
                "Bad range",
                [user2Id],
                [tagId],
                null,
                completedTask.EndDate.Value.AddDays(5),
                ProjectTaskStatus.Completed,
                TaskPriority.High,
                EndDate: null),
            ct);
        invalidPutRangeResp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Null character in task title returns 400 (not 500)
        var nullCharTaskResp = await client2.PutAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}",
            new UpdateTaskCommand(
                topicSmmId,
                "Bad\0Title",
                [user2Id],
                [tagId],
                null,
                taskStartDate,
                ProjectTaskStatus.Completed,
                TaskPriority.High,
                EndDate: completedTask.EndDate),
            ct);
        nullCharTaskResp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Paginated comments endpoint returns PagedResult<TaskCommentDto>
        var pagedComments = await client2.GetFromJsonAsync<PagedResult<TaskCommentDto>>(
            $"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}/comments?pageNumber=1&pageSize=1",
            JsonOptions,
            ct);
        pagedComments.ShouldNotBeNull();
        pagedComments.TotalCount.ShouldBe(2);
        pagedComments.Items.Count.ShouldBe(1);

        // Reopen auto-completed task clears EndDate even after earlier PUT edit
        var reopenResp = await client2.PatchAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}/status",
            new UpdateTaskStatusCommand(ProjectTaskStatus.Blocked),
            ct);
        reopenResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var reopenedTask = await client2.GetFromJsonAsync<TaskDto>(
            $"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}",
            JsonOptions,
            ct);
        reopenedTask.ShouldNotBeNull();
        reopenedTask.Status.ShouldBe(ProjectTaskStatus.Blocked);
        reopenedTask.EndDate.ShouldBeNull();

        // Manual EndDate is preserved across drag-and-drop and reopen
        var manualEndDate = taskStartDate.AddDays(10);
        var manualCompleteResp = await client2.PatchAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}/status",
            new UpdateTaskStatusCommand(ProjectTaskStatus.Completed, EndDate: manualEndDate),
            ct);
        manualCompleteResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var reopenManualResp = await client2.PatchAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}/status",
            new UpdateTaskStatusCommand(ProjectTaskStatus.InProgress),
            ct);
        reopenManualResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterManualReopen = await client2.GetFromJsonAsync<TaskDto>(
            $"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}",
            JsonOptions,
            ct);
        afterManualReopen.ShouldNotBeNull();
        afterManualReopen.EndDate.ShouldBe(manualEndDate);

        // 8. Attachment security (B-14): block dangerous extensions, allow 10MB upload, verify nosniff + attachment
        using (var htmlForm = new MultipartFormDataContent())
        using (var htmlContent = new ByteArrayContent(Encoding.UTF8.GetBytes("<script>alert(1)</script>")))
        {
            htmlContent.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            htmlForm.Add(htmlContent, "file", "xss.html");
            var htmlResp = await client2.PostAsync($"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}/attachments", htmlForm, ct);
            htmlResp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        using (var svgForm = new MultipartFormDataContent())
        using (var svgContent = new ByteArrayContent(Encoding.UTF8.GetBytes("<svg></svg>")))
        {
            svgContent.Headers.ContentType = new MediaTypeHeaderValue("image/svg+xml");
            svgForm.Add(svgContent, "file", "icon.svg");
            var svgResp = await client2.PostAsync($"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}/attachments", svgForm, ct);
            svgResp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        // Exact 10 MB upload must succeed (201, NOT 413 Payload Too Large!)
        var exactTenMbBytes = new byte[TaskValidationConstants.MaxAttachmentFileSize];
        using (var tenMbForm = new MultipartFormDataContent())
        using (var tenMbContent = new ByteArrayContent(exactTenMbBytes))
        {
            tenMbContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            tenMbForm.Add(tenMbContent, "file", "exact-10mb.pdf");
            var tenMbResp = await client2.PostAsync($"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}/attachments", tenMbForm, ct);
            tenMbResp.StatusCode.ShouldBe(HttpStatusCode.Created);
            var uploadedTenMb = await tenMbResp.Content.ReadFromJsonAsync<TaskAttachmentDto>(JsonOptions, ct);
            uploadedTenMb.ShouldNotBeNull();

            // TaskSummaryDto AttachmentsCount reflects 1 attachment (B-19)
            var listWithAttachment = await client2.GetFromJsonAsync<PagedResult<TaskSummaryDto>>(
                $"/api/v1/projects/{projectId}/tasks?filter=mine",
                JsonOptions,
                ct);
            listWithAttachment.ShouldNotBeNull();
            listWithAttachment.Items.Single().AttachmentsCount.ShouldBe(1);

            var downloadResp = await client2.GetAsync(
                $"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}/attachments/{uploadedTenMb.Id}",
                ct);
            downloadResp.StatusCode.ShouldBe(HttpStatusCode.OK);
            downloadResp.Headers.TryGetValues("X-Content-Type-Options", out var nosniff).ShouldBeTrue();
            nosniff.ShouldContain("nosniff");
            downloadResp.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("attachment");

            var delAttachResp = await client2.DeleteAsync(
                $"/api/v1/projects/{projectId}/tasks/{taskForUser2Id}/attachments/{uploadedTenMb.Id}",
                ct);
            delAttachResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        // 9. PUT /tasks/{id} omitting status/priority -> 400 Bad Request (B-11)
        using var incompletePut = new StringContent(
            $$"""{"topicId":"{{topicDevId}}","title":"Incomplete","description":null,"startDate":"2026-11-16T10:00:00Z"}""",
            Encoding.UTF8,
            "application/json");
        var incompletePutResp = await client1.PutAsync($"/api/v1/projects/{projectId}/tasks/{taskForUser1Id}", incompletePut, ct);
        incompletePutResp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Valid PUT updates task
        var updateTaskResp = await client1.PutAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks/{taskForUser1Id}",
            new UpdateTaskCommand(
                topicDevId,
                "Build API v2",
                [user1Id],
                [tagId],
                "Updated description",
                new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc),
                ProjectTaskStatus.InProgress,
                TaskPriority.High),
            ct);
        updateTaskResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PublicPermissionsGlobalTasksUtcDatesConcurrencyAccessRightsAndCleanupShouldWorkEndToEnd()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var databaseName = $"crm_security_{Guid.NewGuid():N}";
        using var factory = fixture.CreateFactory(databaseName);
        var (adminClient, adminId, adminRoleId) = await SeedAdminAndNgoAsync(factory, "admin@ngo.org", "Admin", "One", ct);
        using var adminClientScope = adminClient;
        var (peerClient, peerId) = await InviteAndRegisterUserAsync(factory, adminClient, "peer@ngo.org", "Peer", "Two", adminRoleId, ct);
        using var peerClientScope = peerClient;
        var (inactiveClient, inactiveId) = await InviteAndRegisterUserAsync(factory, adminClient, "inactive@ngo.org", "Inactive", "Three", adminRoleId, ct);
        using var inactiveClientScope = inactiveClient;

        var restrictedRoleResp = await adminClient.PostAsJsonAsync(
            "/api/v1/roles",
            new CreateRoleCommand("UsersOnly", null, null),
            ct);
        restrictedRoleResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var restrictedRoleId = await restrictedRoleResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);
        var (restrictedClient, _) = await InviteAndRegisterUserAsync(factory, adminClient, "limited@ngo.org", "Limited", "Four", restrictedRoleId, ct);
        using var restrictedClientScope = restrictedClient;

        (await restrictedClient.GetAsync("/api/v1/projects", ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await restrictedClient.GetAsync("/api/v1/tasks?filter=mine", ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var deactivateResp = await adminClient.PostAsync($"/api/v1/users/{inactiveId}/deactivate", null, ct);
        deactivateResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var createPublicJson = new StringContent(
            JsonSerializer.Serialize(new
            {
                title = "Public Board",
                description = "Open collaboration",
                visibility = 2,
                startDate = "2026-11-20T00:00:00",
                endDate = "2026-11-20T02:00:00+02:00",
            }),
            Encoding.UTF8,
            "application/json");
        var publicProjResp = await adminClient.PostAsync("/api/v1/projects", createPublicJson, ct);
        publicProjResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var publicProjectId = await publicProjResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        var loadedPublicProj = await adminClient.GetFromJsonAsync<ProjectDto>($"/api/v1/projects/{publicProjectId}", JsonOptions, ct);
        loadedPublicProj.ShouldNotBeNull();
        loadedPublicProj.StartDate.ShouldBe(new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc));
        loadedPublicProj.EndDate.ShouldBe(new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc));

        var assignInactiveResp = await adminClient.PostAsJsonAsync(
            $"/api/v1/projects/{publicProjectId}/members",
            new AssignProjectMemberCommand(inactiveId, "Deactivated", ProjectClaim.View),
            ct);
        assignInactiveResp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var peerTopicResp = await peerClient.PostAsJsonAsync($"/api/v1/projects/{publicProjectId}/topics", new CreateTopicCommand("Open Topic", 0), ct);
        peerTopicResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var publicTopicId = await peerTopicResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        var peerDeleteProjResp = await peerClient.DeleteAsync($"/api/v1/projects/{publicProjectId}", ct);
        peerDeleteProjResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var peerVisibilityResp = await peerClient.PutAsJsonAsync(
            $"/api/v1/projects/{publicProjectId}",
            new UpdateProjectCommand("Public Board", "Open collaboration", ProjectVisibility.Private, new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc)),
            ct);
        peerVisibilityResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var peerAddMemberResp = await peerClient.PostAsJsonAsync(
            $"/api/v1/projects/{publicProjectId}/members",
            new AssignProjectMemberCommand(peerId, "Self", ProjectClaim.Manage),
            ct);
        peerAddMemberResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await adminClient.DeleteAsync($"/api/v1/projects/{publicProjectId}/members/{adminId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await adminClient.PutAsJsonAsync(
                $"/api/v1/projects/{publicProjectId}/members/{adminId}",
                new UpdateProjectMemberCommand("Demoted", ProjectClaim.Edit),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var publicTagResp = await peerClient.PostAsJsonAsync($"/api/v1/projects/{publicProjectId}/tags", new CreateTagCommand("PublicTag"), ct);
        publicTagResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var publicTagId = await publicTagResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        using var publicTaskJson = new StringContent(
            $$"""{"topicId":"{{publicTopicId}}","title":"Peer Public Task","description":null,"startDate":"2026-11-20T00:00:00","status":1,"priority":2,"endDate":"2026-11-20T02:00:00+02:00","assigneeIds":["{{peerId}}"]}""",
            Encoding.UTF8,
            "application/json");
        var publicTaskResp = await peerClient.PostAsync($"/api/v1/projects/{publicProjectId}/tasks", publicTaskJson, ct);
        publicTaskResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var publicTaskId = await publicTaskResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        var parallelAssignTasks = Enumerable.Range(0, 5)
            .Select(_ => peerClient.PostAsync($"/api/v1/projects/{publicProjectId}/tasks/{publicTaskId}/assignees/{adminId}", null, ct));
        var parallelAssignResponses = await Task.WhenAll(parallelAssignTasks);
        parallelAssignResponses.All(r => r.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.Conflict).ShouldBeTrue();
        parallelAssignResponses.Count(r => r.StatusCode == HttpStatusCode.NoContent).ShouldBeGreaterThanOrEqualTo(1);

        var parallelTagTasks = Enumerable.Range(0, 5)
            .Select(_ => peerClient.PostAsync($"/api/v1/projects/{publicProjectId}/tasks/{publicTaskId}/tags/{publicTagId}", null, ct));
        var parallelTagResponses = await Task.WhenAll(parallelTagTasks);
        parallelTagResponses.All(r => r.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.Conflict).ShouldBeTrue();
        parallelTagResponses.Count(r => r.StatusCode == HttpStatusCode.NoContent).ShouldBeGreaterThanOrEqualTo(1);

        string[] raceVariants = ["RaceTag", "racetag", "RACETAG", "RaceTag "];
        var parallelCreateTagResponses = await Task.WhenAll(
            raceVariants.Select(title => adminClient.PostAsJsonAsync($"/api/v1/projects/{publicProjectId}/tags", new CreateTagCommand(title), ct)));
        parallelCreateTagResponses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        parallelCreateTagResponses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(raceVariants.Length - 1);

        var peerPrivateProjId = await CreateProjectAsync(adminClient, "Peer Private", ProjectVisibility.Private, ct);
        (await adminClient.PostAsJsonAsync(
                $"/api/v1/projects/{peerPrivateProjId}/members",
                new AssignProjectMemberCommand(peerId, "Dev", ProjectClaim.Edit),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        var peerPrivateTopicId = await CreateTopicAsync(adminClient, peerPrivateProjId, "Private Col", ct);
        var peerPrivateTaskResp = await adminClient.PostAsJsonAsync(
            $"/api/v1/projects/{peerPrivateProjId}/tasks",
            new CreateTaskCommand(peerPrivateTopicId, "Peer Private Task", [peerId], [], null, new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc)),
            ct);
        peerPrivateTaskResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var peerPrivateTaskId = await peerPrivateTaskResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        var secretProjId = await CreateProjectAsync(adminClient, "Secret Private", ProjectVisibility.Private, ct);
        var secretTopicId = await CreateTopicAsync(adminClient, secretProjId, "Secret Col", ct);
        (await adminClient.PostAsJsonAsync(
                $"/api/v1/projects/{secretProjId}/tasks",
                new CreateTaskCommand(secretTopicId, "Admin Secret Task", [adminId], [], null, new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc)),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var globalMine = await peerClient.GetFromJsonAsync<PagedResult<TaskSummaryDto>>(
            "/api/v1/tasks?filter=mine&startDateFrom=2026-11-20T00:00:00&startDateTo=2026-11-20T02:00:00%2B02:00",
            JsonOptions,
            ct);
        globalMine.ShouldNotBeNull();
        globalMine.TotalCount.ShouldBe(2);
        globalMine.Items.Select(t => t.Id).ShouldContain(publicTaskId);
        globalMine.Items.Select(t => t.Id).ShouldContain(peerPrivateTaskId);

        var globalVisible = await peerClient.GetFromJsonAsync<PagedResult<TaskSummaryDto>>("/api/v1/tasks", JsonOptions, ct);
        globalVisible.ShouldNotBeNull();
        globalVisible.Items.ShouldNotContain(t => t.ProjectId == secretProjId);

        (await adminClient.PostAsJsonAsync(
                $"/api/v1/projects/{publicProjectId}/tasks/{publicTaskId}/comments",
                new CreateTaskCommentCommand("Unread notification before privacy change"),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var makePrivateResp = await adminClient.PutAsJsonAsync(
            $"/api/v1/projects/{publicProjectId}",
            new UpdateProjectCommand("Public Board", "Now Private", ProjectVisibility.Private, new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc)),
            ct);
        makePrivateResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var publicTaskAfterPrivate = await adminClient.GetFromJsonAsync<TaskDto>(
            $"/api/v1/projects/{publicProjectId}/tasks/{publicTaskId}",
            JsonOptions,
            ct);
        publicTaskAfterPrivate.ShouldNotBeNull();
        publicTaskAfterPrivate.AssignedUsers.Select(u => u.Id).ShouldNotContain(peerId);
        publicTaskAfterPrivate.AssignedUsers.Select(u => u.Id).ShouldContain(adminId);

        (await adminClient.DeleteAsync($"/api/v1/projects/{peerPrivateProjId}/members/{peerId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var peerPrivateTaskAfterRemoval = await adminClient.GetFromJsonAsync<TaskDto>(
            $"/api/v1/projects/{peerPrivateProjId}/tasks/{peerPrivateTaskId}",
            JsonOptions,
            ct);
        peerPrivateTaskAfterRemoval.ShouldNotBeNull();
        peerPrivateTaskAfterRemoval.AssignedUsers.ShouldBeEmpty();

        var peerGlobalAfterCleanup = await peerClient.GetFromJsonAsync<PagedResult<TaskSummaryDto>>("/api/v1/tasks?filter=mine", JsonOptions, ct);
        peerGlobalAfterCleanup.ShouldNotBeNull();
        peerGlobalAfterCleanup.TotalCount.ShouldBe(0);

        var soleManagerProjId = await CreateProjectAsync(peerClient, "Peer Sole Private", ProjectVisibility.Private, ct);
        (await peerClient.DeleteAsync("/api/v1/users/me", ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await peerClient.PostAsJsonAsync(
                $"/api/v1/projects/{soleManagerProjId}/members",
                new AssignProjectMemberCommand(adminId, "New Owner", ProjectClaim.Manage),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        (await adminClient.DeleteAsync($"/api/v1/projects/{soleManagerProjId}/members/{peerId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await peerClient.GetAsync($"/api/v1/projects/{soleManagerProjId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await peerClient.DeleteAsync($"/api/v1/projects/{soleManagerProjId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var raceProjId = await CreateProjectAsync(adminClient, "Manager Race", ProjectVisibility.Private, ct);
        (await adminClient.PostAsJsonAsync(
                $"/api/v1/projects/{raceProjId}/members",
                new AssignProjectMemberCommand(peerId, "Co-Manager", ProjectClaim.Manage),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var raceResponses = await Task.WhenAll(
            adminClient.DeleteAsync($"/api/v1/projects/{raceProjId}/members/{peerId}", ct),
            peerClient.DeleteAsync($"/api/v1/projects/{raceProjId}/members/{adminId}", ct));
        raceResponses.Count(r => r.StatusCode == HttpStatusCode.NoContent).ShouldBe(1);
        raceResponses.Count(r => r.StatusCode == HttpStatusCode.BadRequest).ShouldBe(1);

        var handoffOnDeactivateProjId = await CreateProjectAsync(peerClient, "Peer Deactivate Handoff", ProjectVisibility.Private, ct);
        (await adminClient.GetAsync($"/api/v1/projects/{handoffOnDeactivateProjId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await adminClient.PostAsync($"/api/v1/users/{peerId}/deactivate", null, ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var handedOffOnDeactivate = await adminClient.GetFromJsonAsync<ProjectDto>($"/api/v1/projects/{handoffOnDeactivateProjId}", JsonOptions, ct);
        handedOffOnDeactivate.ShouldNotBeNull();
        handedOffOnDeactivate.Members.ShouldContain(m => m.UserId == adminId && m.Claim == ProjectClaim.Manage);

        (await adminClient.PostAsync($"/api/v1/users/{peerId}/activate", null, ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var handoffOnDeleteProjId = await CreateProjectAsync(peerClient, "Peer Delete Handoff", ProjectVisibility.Private, ct);
        (await adminClient.GetAsync($"/api/v1/projects/{handoffOnDeleteProjId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await adminClient.DeleteAsync($"/api/v1/users/{peerId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var handedOffOnDelete = await adminClient.GetFromJsonAsync<ProjectDto>($"/api/v1/projects/{handoffOnDeleteProjId}", JsonOptions, ct);
        handedOffOnDelete.ShouldNotBeNull();
        handedOffOnDelete.Members.ShouldContain(m => m.UserId == adminId && m.Claim == ProjectClaim.Manage);
    }

    private static async Task<(HttpClient Client, Guid UserId, Guid AdminRoleId)> SeedAdminAndNgoAsync(
        WebApplicationFactory<Program> factory,
        string email,
        string firstName,
        string lastName,
        CancellationToken ct)
    {
        var client = factory.CreateClient();
        var createUserResp = await client.PostAsJsonAsync(
            "/api/v1/users",
            new CreateUserCommand(firstName, lastName, null, email, "StrongPass1!", "StrongPass1!"),
            ct);
        createUserResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var userId = await createUserResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        var loginResp = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new AuthenticateUserCommand(email, "StrongPass1!"),
            ct);
        loginResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tokens = await loginResp.Content.ReadFromJsonAsync<AuthTokensDto>(JsonOptions, ct);
        tokens.ShouldNotBeNull();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var ngoResp = await client.PostAsJsonAsync("/api/v1/ngo", new CreateNgoCommand("Test NGO"), ct);
        ngoResp.StatusCode.ShouldBe(HttpStatusCode.Created);

        var rolesResp = await client.GetFromJsonAsync<PagedResult<RoleDto>>("/api/v1/roles", JsonOptions, ct);
        rolesResp.ShouldNotBeNull();
        return (client, userId, rolesResp.Items.First().Id);
    }

    private static async Task<(HttpClient Client, Guid UserId)> InviteAndRegisterUserAsync(
        WebApplicationFactory<Program> factory,
        HttpClient adminClient,
        string email,
        string firstName,
        string lastName,
        Guid roleId,
        CancellationToken ct)
    {
        var inviteResp = await adminClient.PostAsJsonAsync(
            "/api/v1/invitations",
            new InviteUserCommand(email, [roleId]),
            ct);
        inviteResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var inviteToken = await inviteResp.Content.ReadFromJsonAsync<TokenResultDto>(JsonOptions, ct);
        inviteToken.ShouldNotBeNull();

        var client = factory.CreateClient();
        var acceptResp = await client.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new CompleteRegistrationCommand(inviteToken.Token, firstName, lastName, null, "StrongPass2!", "StrongPass2!"),
            ct);
        acceptResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tokens = await acceptResp.Content.ReadFromJsonAsync<AuthTokensDto>(JsonOptions, ct);
        tokens.ShouldNotBeNull();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var profile = await client.GetFromJsonAsync<UserProfileDto>("/api/v1/users/me", JsonOptions, ct);
        profile.ShouldNotBeNull();
        return (client, profile.Id);
    }

    private static async Task<Guid> CreateProjectAsync(
        HttpClient client,
        string title,
        ProjectVisibility visibility,
        CancellationToken ct)
    {
        var resp = await client.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectCommand(title, null, visibility, new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc)),
            ct);
        resp.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await resp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);
    }

    private static async Task<Guid> CreateTopicAsync(
        HttpClient client,
        Guid projectId,
        string title,
        CancellationToken ct)
    {
        var resp = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/topics",
            new CreateTopicCommand(title, 0),
            ct);
        resp.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await resp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);
    }

    public sealed class ApiPostgresFixture : IAsyncLifetime
    {
        private readonly List<WebApplicationFactory<Program>> _factories = [];
        private PostgreSqlContainer? _container;
        private string _tempUploadDir = string.Empty;

        public bool IsDockerAvailable { get; private set; }

        public WebApplicationFactory<Program> CreateFactory(
            string databaseName,
            IDictionary<string, string?>? extraConfig = null)
        {
            ArgumentNullException.ThrowIfNull(_container);
            var connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
            {
                Database = databaseName,
            }.ConnectionString;
            var uploadDir = _tempUploadDir;

            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSqlConnection"] = connectionString,
                ["JwtOptions:Secret"] = "integration-test-jwt-secret-key-0123456789abcdef",
                ["FileStorage:UploadDirectory"] = uploadDir,
                ["FileStorage:BaseUrl"] = "http://localhost",
                ["CorsSettings:AllowedOrigins:0"] = "http://localhost",
                ["RateLimiting:PermitLimit"] = "10000",
            };

            if (extraConfig is not null)
            {
                foreach (var kvp in extraConfig)
                {
                    settings[kvp.Key] = kvp.Value;
                }
            }

            var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Production");
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(settings);
                    });
                });

            _factories.Add(factory);
            return factory;
        }

        public async ValueTask InitializeAsync()
        {
            try
            {
                _tempUploadDir = Path.Combine(Path.GetTempPath(), $"crm-tests-{Guid.NewGuid():N}");
                Directory.CreateDirectory(_tempUploadDir);

                _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
                await _container.StartAsync();
                IsDockerAvailable = true;
            }
            catch (Exception)
            {
                IsDockerAvailable = false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var factory in _factories)
            {
                await factory.DisposeAsync();
            }

            if (_container is not null)
            {
                await _container.DisposeAsync();
            }

            if (!string.IsNullOrEmpty(_tempUploadDir) && Directory.Exists(_tempUploadDir))
            {
                Directory.Delete(_tempUploadDir, recursive: true);
            }
        }
    }
}
