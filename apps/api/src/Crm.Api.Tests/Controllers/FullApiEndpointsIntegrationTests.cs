using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.CustomRecords;
using Crm.Application.Dtos.CustomTables;
using Crm.Application.Dtos.LoginPageImages;
using Crm.Application.Dtos.Projects;
using Crm.Application.Dtos.Roles;
using Crm.Application.Dtos.TableTemplates;
using Crm.Application.Dtos.Tasks;
using Crm.Application.Dtos.Users;
using Crm.Application.Features.CustomRecords.Commands;
using Crm.Application.Features.CustomTables.Commands;
using Crm.Application.Features.Ngos.Commands;
using Crm.Application.Features.Projects.Commands;
using Crm.Application.Features.Roles.Commands;
using Crm.Application.Features.TableTemplates.Commands;
using Crm.Application.Features.Tasks.Commands;
using Crm.Application.Features.Users.Commands;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Extensions;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Crm.Api.Tests.Controllers;

/// <summary>
/// End-to-end PostgreSQL integration tests covering previously untested API endpoints,
/// auth/invitation security edge cases, soft-delete vs hard-delete semantics, and invalid image handling.
/// </summary>
public sealed class FullApiEndpointsIntegrationTests(ProjectsAndTasksEndpointsIntegrationTests.ApiPostgresFixture fixture)
    : IClassFixture<ProjectsAndTasksEndpointsIntegrationTests.ApiPostgresFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Valid 1x1 transparent PNG image for avatar and login-page-image upload tests.
    private static readonly byte[] ValidPngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    [Fact]
    public async Task HealthAuthInvitationsTemplatesTablesRecordsImagesRolesAndUsersEndpointsShouldWorkEndToEnd()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        using var factory = fixture.CreateFactory($"crm_full_api_{Guid.NewGuid():N}");

        // 1. Health endpoint
        var anonClient = factory.CreateClient();
        var healthResp = await anonClient.GetAsync("/health", ct);
        healthResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // 2. Seed initial admin + NGO + login + auth edge cases
        var createAdminResp = await anonClient.PostAsJsonAsync(
            "/api/v1/users",
            new CreateUserCommand("Admin", "Owner", null, "full-admin@ngo.org", "StrongPass1!", "StrongPass1!"),
            ct);
        createAdminResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var adminId = await createAdminResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        // Wrong password -> 401
        var wrongLoginResp = await anonClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new AuthenticateUserCommand("full-admin@ngo.org", "WrongPassword1!"),
            ct);
        wrongLoginResp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Valid login -> 200
        var loginResp = await anonClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new AuthenticateUserCommand("full-admin@ngo.org", "StrongPass1!"),
            ct);
        loginResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var initialTokens = (await loginResp.Content.ReadFromJsonAsync<AuthTokensDto>(JsonOptions, ct))!;

        // Refresh token rotation + replay rejection
        var refreshResp = await anonClient.PostAsJsonAsync(
            "/api/v1/auth/refresh-token",
            new RefreshTokensCommand(initialTokens.AccessToken, initialTokens.RefreshToken),
            ct);
        refreshResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rotatedTokens = (await refreshResp.Content.ReadFromJsonAsync<AuthTokensDto>(JsonOptions, ct))!;

        var replayRefreshResp = await anonClient.PostAsJsonAsync(
            "/api/v1/auth/refresh-token",
            new RefreshTokensCommand(initialTokens.AccessToken, initialTokens.RefreshToken),
            ct);
        replayRefreshResp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Logout revokes active refresh token
        var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", rotatedTokens.AccessToken);
        (await adminClient.PostAsJsonAsync("/api/v1/auth/logout", new LogoutCommand(rotatedTokens.RefreshToken), ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await anonClient.PostAsJsonAsync(
                "/api/v1/auth/refresh-token",
                new RefreshTokensCommand(rotatedTokens.AccessToken, rotatedTokens.RefreshToken),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Invalid invitation token -> 401/400
        var invalidInviteResp = await anonClient.GetAsync("/api/v1/invitations/validate?Token=not-a-valid-jwt", ct);
        ((int)invalidInviteResp.StatusCode).ShouldBeOneOf(400, 401);

        // Create NGO
        (await adminClient.PostAsJsonAsync("/api/v1/ngo", new CreateNgoCommand("Full Coverage NGO"), ct))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        // 3. TableTemplates: POST, GET /{id}, PUT /{id}, DELETE /{id}
        var createTplResp = await adminClient.PostAsJsonAsync(
            "/api/v1/tabletemplates",
            new CreateTableTemplateCommand(
                "Grant Template",
                "Initial desc",
                [new CreateTemplateFieldDto("Title", "title", FieldDataType.Text, true, 0)]),
            ct);
        createTplResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var templateId = await createTplResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        var fetchedTemplate = await adminClient.GetFromJsonAsync<TableTemplateDto>($"/api/v1/tabletemplates/{templateId}", JsonOptions, ct);
        fetchedTemplate.ShouldNotBeNull();
        fetchedTemplate.Name.ShouldBe("Grant Template");
        var templateFieldId = fetchedTemplate.Fields.ShouldHaveSingleItem().Id;

        (await adminClient.PutAsJsonAsync(
                $"/api/v1/tabletemplates/{templateId}",
                new UpdateTableTemplateCommand(
                    "Grant Template Updated",
                    "Updated desc",
                    [new UpdateTemplateFieldDto(templateFieldId, "Title Updated", "title", FieldDataType.Text, true, 0)]),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var updatedTemplate = await adminClient.GetFromJsonAsync<TableTemplateDto>($"/api/v1/tabletemplates/{templateId}", JsonOptions, ct);
        updatedTemplate!.Name.ShouldBe("Grant Template Updated");
        (await adminClient.DeleteAsync($"/api/v1/tabletemplates/{templateId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 4. CustomTables & CustomRecords: GET /{id}, PUT /{id}, SearchTerm, HARD-DELETE record, SOFT-DELETE table
        var createTableResp = await adminClient.PostAsJsonAsync(
            "/api/v1/customtables",
            new CreateCustomTableCommand(
                "Donors",
                "Active donors",
                [new CreateCustomFieldDto("Organization", "org_name", FieldDataType.Text, true, 0)]),
            ct);
        createTableResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var tableId = await createTableResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        var rec1Resp = await adminClient.PostAsJsonAsync(
            "/api/v1/customrecords",
            new CreateCustomRecordCommand(tableId, new JsonObject { ["org_name"] = "Kyiv Aid Foundation" }),
            ct);
        rec1Resp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var record1Id = await rec1Resp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        var rec2Resp = await adminClient.PostAsJsonAsync(
            "/api/v1/customrecords",
            new CreateCustomRecordCommand(tableId, new JsonObject { ["org_name"] = "Lviv Relief" }),
            ct);
        rec2Resp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var record2Id = await rec2Resp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        var fetchedRecord = await adminClient.GetFromJsonAsync<CustomRecordDto>($"/api/v1/customrecords/{record1Id}", JsonOptions, ct);
        fetchedRecord.ShouldNotBeNull();
        fetchedRecord.Data["org_name"]?.GetValue<string>().ShouldBe("Kyiv Aid Foundation");

        (await adminClient.PutAsJsonAsync(
                $"/api/v1/customrecords/{record1Id}",
                new UpdateCustomRecordCommand(new JsonObject { ["org_name"] = "Kyiv Humanitarian Hub" }),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var searchRecords = await adminClient.GetFromJsonAsync<PagedResult<CustomRecordDto>>(
            $"/api/v1/customrecords?CustomTableId={tableId}&SearchTerm=Humanitarian",
            JsonOptions,
            ct);
        searchRecords!.Items.ShouldHaveSingleItem().Id.ShouldBe(record1Id);

        // Hard-delete CustomRecord -> row is physically removed from DB
        (await adminClient.DeleteAsync($"/api/v1/customrecords/{record1Id}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Set<CustomRecord>().IgnoreQueryFilters().AnyAsync(r => r.Id == record1Id, ct))
                .ShouldBeFalse();
        }

        // Soft-delete CustomTable -> table marked IsDeleted=true, remaining record hidden by global filter
        (await adminClient.DeleteAsync($"/api/v1/customtables/{tableId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await adminClient.GetAsync($"/api/v1/customtables/{tableId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await adminClient.GetAsync($"/api/v1/customrecords/{record2Id}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var softDeletedTable = await db.Set<CustomTable>().IgnoreQueryFilters().SingleAsync(t => t.Id == tableId, ct);
            softDeletedTable.IsDeleted.ShouldBeTrue();
            softDeletedTable.DeletedAt.ShouldNotBeNull();
            (await db.Set<CustomRecord>().IgnoreQueryFilters().AnyAsync(r => r.Id == record2Id, ct))
                .ShouldBeTrue();
        }

        // 5. LoginPageImages: invalid image -> 400, valid upload -> 201, GET /{id} -> 200, DELETE /{id} -> 204
        using (var badImgForm = CreateMultipartFile("file", "corrupt.png", "image/png", Encoding.UTF8.GetBytes("not-a-png")))
        {
            var badImgResp = await adminClient.PostAsync("/api/v1/loginpageimages", badImgForm, ct);
            badImgResp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        Guid loginImageId;
        using (var validImgForm = CreateMultipartFile("file", "hero.png", "image/png", ValidPngBytes))
        {
            var uploadImgResp = await adminClient.PostAsync("/api/v1/loginpageimages", validImgForm, ct);
            uploadImgResp.StatusCode.ShouldBe(HttpStatusCode.Created);
            loginImageId = await uploadImgResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);
        }

        var fetchedImg = await adminClient.GetFromJsonAsync<LoginPageImageDto>($"/api/v1/loginpageimages/{loginImageId}", JsonOptions, ct);
        fetchedImg!.Id.ShouldBe(loginImageId);
        (await adminClient.DeleteAsync($"/api/v1/loginpageimages/{loginImageId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 6. Roles: POST, GET /{id}, PUT /{id}, POST/DELETE claims, DELETE /{id}
        var createRoleResp = await adminClient.PostAsJsonAsync(
            "/api/v1/roles",
            new CreateRoleCommand("Coordinator", "Координаторка", "Координатори"),
            ct);
        createRoleResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var customRoleId = await createRoleResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        var roleDetails = await adminClient.GetFromJsonAsync<RoleDetailsDto>($"/api/v1/roles/{customRoleId}", JsonOptions, ct);
        roleDetails!.Name.ShouldBe("Coordinator");

        (await adminClient.PutAsJsonAsync(
                $"/api/v1/roles/{customRoleId}",
                new UpdateRoleCommand("Lead Coordinator", "Головна координаторка", "Головні координатори"),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        string viewUserClaim = AccessRight.ViewUser.ToClaimValue();
        (await adminClient.PostAsync($"/api/v1/roles/{customRoleId}/claims/{viewUserClaim}", null, ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await adminClient.DeleteAsync($"/api/v1/roles/{customRoleId}/claims/{viewUserClaim}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 7. Users: invite secondary user, test admin role/avatar/custom-field endpoints, self custom-fields, last-admin guard, deactivated login, DELETE /me
        var inviteResp = await adminClient.PostAsJsonAsync(
            "/api/v1/invitations",
            new InviteUserCommand("member@ngo.org", [customRoleId]),
            ct);
        inviteResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var inviteToken = (await inviteResp.Content.ReadFromJsonAsync<TokenResultDto>(JsonOptions, ct))!;

        var memberClient = factory.CreateClient();
        var acceptResp = await memberClient.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new CompleteRegistrationCommand(inviteToken.Token, "Olena", "Petrenko", null, "StrongPass2!", "StrongPass2!"),
            ct);
        acceptResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var memberTokens = (await acceptResp.Content.ReadFromJsonAsync<AuthTokensDto>(JsonOptions, ct))!;
        memberClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberTokens.AccessToken);

        var memberProfile = (await memberClient.GetFromJsonAsync<UserProfileDto>("/api/v1/users/me", JsonOptions, ct))!;
        var memberId = memberProfile.Id;

        // Admin assigns & removes extra role on user
        var extraRoleResp = await adminClient.PostAsJsonAsync("/api/v1/roles", new CreateRoleCommand("Volunteer", null, null), ct);
        extraRoleResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var extraRoleId = await extraRoleResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        (await adminClient.PostAsync($"/api/v1/users/{memberId}/roles/{extraRoleId}", null, ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await adminClient.DeleteAsync($"/api/v1/users/{memberId}/roles/{extraRoleId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await adminClient.DeleteAsync($"/api/v1/roles/{extraRoleId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Admin uploads & retrieves user avatar (plus invalid format -> 400)
        using (var badAvatarForm = CreateMultipartFile("file", "bad.png", "image/png", Encoding.UTF8.GetBytes("invalid")))
        {
            (await adminClient.PostAsync($"/api/v1/users/{memberId}/avatar", badAvatarForm, ct))
                .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        using (var avatarForm = CreateMultipartFile("file", "avatar.png", "image/png", ValidPngBytes))
        {
            (await adminClient.PostAsync($"/api/v1/users/{memberId}/avatar", avatarForm, ct))
                .StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await adminClient.GetAsync($"/api/v1/users/{memberId}/avatar", ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // Admin adds, updates, removes custom field on target user
        (await adminClient.PostAsJsonAsync(
                $"/api/v1/users/{memberId}/custom-fields",
                new AddUserCustomFieldCommand("telegram", "@olena", true),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var memberWithField = (await adminClient.GetFromJsonAsync<UserProfileDto>($"/api/v1/users/{memberId}", JsonOptions, ct))!;
        var adminAddedFieldId = memberWithField.CustomFields.ShouldHaveSingleItem().Id;

        (await adminClient.PutAsJsonAsync(
                $"/api/v1/users/{memberId}/custom-fields/{adminAddedFieldId}",
                new UpdateUserCustomFieldCommand("@olena_updated", false),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await adminClient.DeleteAsync($"/api/v1/users/{memberId}/custom-fields/{adminAddedFieldId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Self user adds, updates, removes own custom field
        (await memberClient.PostAsJsonAsync(
                "/api/v1/users/me/custom-fields",
                new AddUserCustomFieldCommand("city", "Kyiv", true),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var selfProfile = (await memberClient.GetFromJsonAsync<UserProfileDto>("/api/v1/users/me", JsonOptions, ct))!;
        var selfFieldId = selfProfile.CustomFields.ShouldHaveSingleItem().Id;

        (await memberClient.PutAsJsonAsync(
                $"/api/v1/users/me/custom-fields/{selfFieldId}",
                new UpdateUserCustomFieldCommand("Lviv", true),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await memberClient.DeleteAsync($"/api/v1/users/me/custom-fields/{selfFieldId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Last admin protection -> 400
        (await adminClient.PostAsync($"/api/v1/users/{adminId}/deactivate", null, ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await adminClient.DeleteAsync("/api/v1/users/me", ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Deactivated user immediately loses access even on role-protected endpoints (GET /api/v1/users -> 401, not 200),
        // as well as /me, refresh-token, and login.
        (await adminClient.PostAsync($"/api/v1/roles/{customRoleId}/claims/{viewUserClaim}", null, ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var usersBeforeDeactivation = await memberClient.GetFromJsonAsync<PagedResult<UserDto>>("/api/v1/users", JsonOptions, ct);
        usersBeforeDeactivation!.TotalCount.ShouldBe(2);

        (await adminClient.PostAsync($"/api/v1/users/{memberId}/deactivate", null, ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await memberClient.GetAsync("/api/v1/users", ct))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await memberClient.GetAsync("/api/v1/users/me", ct))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await memberClient.GetAsync("/api/v1/users/me/permissions", ct))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var deactivatedRefreshResp = await anonClient.PostAsJsonAsync(
            "/api/v1/auth/refresh-token",
            new RefreshTokensCommand(memberTokens.AccessToken, memberTokens.RefreshToken),
            ct);
        deactivatedRefreshResp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var deactivatedLoginResp = await anonClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new AuthenticateUserCommand("member@ngo.org", "StrongPass2!"),
            ct);
        deactivatedLoginResp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Re-activating removes user from revoked cache; old pre-deactivation refresh token stays revoked, fresh login succeeds
        (await adminClient.PostAsync($"/api/v1/users/{memberId}/activate", null, ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await anonClient.PostAsJsonAsync(
                "/api/v1/auth/refresh-token",
                new RefreshTokensCommand(memberTokens.AccessToken, memberTokens.RefreshToken),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var reactivatedLoginResp = await anonClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new AuthenticateUserCommand("member@ngo.org", "StrongPass2!"),
            ct);
        reactivatedLoginResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var freshMemberTokens = (await reactivatedLoginResp.Content.ReadFromJsonAsync<AuthTokensDto>(JsonOptions, ct))!;
        memberClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", freshMemberTokens.AccessToken);

        (await memberClient.GetAsync("/api/v1/users/me", ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await memberClient.GetAsync("/api/v1/users", ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // Self-delete (DELETE /api/v1/users/me) for non-last-admin succeeds and immediately revokes active access token (401)
        (await memberClient.DeleteAsync("/api/v1/users/me", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await memberClient.GetAsync("/api/v1/users", ct))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await memberClient.GetAsync("/api/v1/users/me", ct))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await memberClient.GetAsync("/api/v1/users/me/permissions", ct))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonClient.PostAsJsonAsync(
                "/api/v1/auth/refresh-token",
                new RefreshTokensCommand(freshMemberTokens.AccessToken, freshMemberTokens.RefreshToken),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Admin hard-delete (DELETE /api/v1/users/{id}) also immediately revokes the deleted user's active access token on role-protected endpoints
        var secondInviteResp = await adminClient.PostAsJsonAsync(
            "/api/v1/invitations",
            new InviteUserCommand("member2@ngo.org", [customRoleId]),
            ct);
        secondInviteResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondInviteToken = (await secondInviteResp.Content.ReadFromJsonAsync<TokenResultDto>(JsonOptions, ct))!;

        var secondMemberClient = factory.CreateClient();
        var secondAcceptResp = await secondMemberClient.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new CompleteRegistrationCommand(secondInviteToken.Token, "Petro", "Koval", null, "StrongPass3!", "StrongPass3!"),
            ct);
        secondAcceptResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondTokens = (await secondAcceptResp.Content.ReadFromJsonAsync<AuthTokensDto>(JsonOptions, ct))!;
        secondMemberClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondTokens.AccessToken);

        var secondProfile = (await secondMemberClient.GetFromJsonAsync<UserProfileDto>("/api/v1/users/me", JsonOptions, ct))!;
        (await secondMemberClient.GetFromJsonAsync<PagedResult<UserDto>>("/api/v1/users", JsonOptions, ct))!.TotalCount.ShouldBe(2);

        (await adminClient.DeleteAsync($"/api/v1/users/{secondProfile.Id}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await secondMemberClient.GetAsync("/api/v1/users", ct))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await secondMemberClient.GetAsync("/api/v1/users/me", ct))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonClient.PostAsJsonAsync(
                "/api/v1/auth/refresh-token",
                new RefreshTokensCommand(secondTokens.AccessToken, secondTokens.RefreshToken),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Control check: acting admin retains GET /api/v1/users access with 1 user remaining
        (await adminClient.GetFromJsonAsync<PagedResult<UserDto>>("/api/v1/users", JsonOptions, ct))!.TotalCount.ShouldBe(1);

        (await adminClient.DeleteAsync($"/api/v1/roles/{customRoleId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 8. Projects & ProjectTasks: remaining endpoints + author PUT comment 204 + SOFT DELETE ProjectTask & Project
        var projectResp = await adminClient.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectCommand("Soft Delete Project", "Desc", ProjectVisibility.Private, new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc)),
            ct);
        projectResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var projectId = await projectResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        // Creating first topic with non-zero order is rejected (400)
        (await adminClient.PostAsJsonAsync($"/api/v1/projects/{projectId}/topics", new CreateTopicCommand("Invalid Start", 1), ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var topicResp = await adminClient.PostAsJsonAsync($"/api/v1/projects/{projectId}/topics", new CreateTopicCommand("Backlog", 0), ct);
        topicResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var topicId = await topicResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        var emptyTopicResp = await adminClient.PostAsJsonAsync($"/api/v1/projects/{projectId}/topics", new CreateTopicCommand("Temp", 1), ct);
        emptyTopicResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var emptyTopicId = await emptyTopicResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        var thirdTopicResp = await adminClient.PostAsJsonAsync($"/api/v1/projects/{projectId}/topics", new CreateTopicCommand("Review", 2), ct);
        thirdTopicResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var thirdTopicId = await thirdTopicResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        // Updating topic to out-of-bounds order is rejected (400)
        (await adminClient.PutAsJsonAsync($"/api/v1/projects/{projectId}/topics/{emptyTopicId}", new UpdateTopicCommand("Temp", 5), ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Reordering Temp (1 -> 0) shifts Backlog (0 -> 1) cleanly without unique constraint collision
        (await adminClient.PutAsJsonAsync($"/api/v1/projects/{projectId}/topics/{emptyTopicId}", new UpdateTopicCommand("Temp Renamed", 0), ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var reorderedTopics = await adminClient.GetFromJsonAsync<PagedResult<TopicDto>>($"/api/v1/projects/{projectId}/topics", JsonOptions, ct);
        reorderedTopics!.Items.Select(t => (t.Id, t.Order)).ShouldBe([(emptyTopicId, (byte)0), (topicId, (byte)1), (thirdTopicId, (byte)2)]);

        // Deleting order 0 (Temp Renamed) compacts remaining topics to [0, 1]
        (await adminClient.DeleteAsync($"/api/v1/projects/{projectId}/topics/{emptyTopicId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var compactedTopics = await adminClient.GetFromJsonAsync<PagedResult<TopicDto>>($"/api/v1/projects/{projectId}/topics", JsonOptions, ct);
        compactedTopics!.Items.Select(t => (t.Id, t.Order)).ShouldBe([(topicId, (byte)0), (thirdTopicId, (byte)1)]);

        var tagResp = await adminClient.PostAsJsonAsync($"/api/v1/projects/{projectId}/tags", new CreateTagCommand("Backend"), ct);
        tagResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var tagId = await tagResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        (await adminClient.PutAsJsonAsync($"/api/v1/projects/{projectId}/tags/{tagId}", new UpdateTagCommand("Backend API"), ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var taskResp = await adminClient.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks",
            new CreateTaskCommand(topicId, "Initial Task", null, null, null, new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc), ProjectTaskStatus.ToDo, TaskPriority.Medium),
            ct);
        taskResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var taskId = await taskResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        // Assign/unassign user & add/remove tag on task
        (await adminClient.PostAsync($"/api/v1/projects/{projectId}/tasks/{taskId}/assignees/{adminId}", null, ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await adminClient.DeleteAsync($"/api/v1/projects/{projectId}/tasks/{taskId}/assignees/{adminId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await adminClient.PostAsync($"/api/v1/projects/{projectId}/tasks/{taskId}/tags/{tagId}", null, ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await adminClient.DeleteAsync($"/api/v1/projects/{projectId}/tasks/{taskId}/tags/{tagId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await adminClient.DeleteAsync($"/api/v1/projects/{projectId}/tags/{tagId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Update task & PATCH status to Done (auto EndDate)
        (await adminClient.PutAsJsonAsync(
                $"/api/v1/projects/{projectId}/tasks/{taskId}",
                new UpdateTaskCommand(topicId, "Updated Task", [adminId], [], "Updated body", new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc), ProjectTaskStatus.InProgress, TaskPriority.High),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await adminClient.PatchAsJsonAsync(
                $"/api/v1/projects/{projectId}/tasks/{taskId}/status",
                new UpdateTaskStatusCommand(ProjectTaskStatus.Completed),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var doneTask = (await adminClient.GetFromJsonAsync<TaskDto>($"/api/v1/projects/{projectId}/tasks/{taskId}", JsonOptions, ct))!;
        doneTask.Status.ShouldBe(ProjectTaskStatus.Completed);
        doneTask.EndDate.ShouldNotBeNull();
        doneTask.EndDateWasAuto.ShouldBeTrue();

        // Author updates own comment (204)
        var commentResp = await adminClient.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks/{taskId}/comments",
            new CreateTaskCommentCommand("Original comment"),
            ct);
        commentResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var commentId = await commentResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        (await adminClient.PutAsJsonAsync(
                $"/api/v1/projects/{projectId}/tasks/{taskId}/comments/{commentId}",
                new UpdateTaskCommentCommand("Edited by author"),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Task attachments: POST, GET list, GET download, DELETE
        TaskAttachmentDto attachment;
        using (var attForm = CreateMultipartFile("file", "notes.txt", "text/plain", Encoding.UTF8.GetBytes("attachment-body")))
        {
            var attResp = await adminClient.PostAsync($"/api/v1/projects/{projectId}/tasks/{taskId}/attachments", attForm, ct);
            attResp.StatusCode.ShouldBe(HttpStatusCode.Created);
            attachment = (await attResp.Content.ReadFromJsonAsync<TaskAttachmentDto>(JsonOptions, ct))!;
        }

        var attachmentsList = await adminClient.GetFromJsonAsync<List<TaskAttachmentDto>>(
            $"/api/v1/projects/{projectId}/tasks/{taskId}/attachments",
            JsonOptions,
            ct);
        attachmentsList!.ShouldHaveSingleItem().Id.ShouldBe(attachment.Id);

        var downloadResp = await adminClient.GetAsync(
            $"/api/v1/projects/{projectId}/tasks/{taskId}/attachments/{attachment.Id}",
            ct);
        downloadResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await downloadResp.Content.ReadAsStringAsync(ct)).ShouldBe("attachment-body");

        (await adminClient.DeleteAsync($"/api/v1/projects/{projectId}/tasks/{taskId}/attachments/{attachment.Id}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Soft-delete Task -> IsDeleted=true in DB, 404 via API
        (await adminClient.DeleteAsync($"/api/v1/projects/{projectId}/tasks/{taskId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await adminClient.GetAsync($"/api/v1/projects/{projectId}/tasks/{taskId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Create a second active task on the project before soft-deleting the whole project
        var secondTaskResp = await adminClient.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks",
            new CreateTaskCommand(topicId, "Child Of Deleted Project", null, null, null, new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc), ProjectTaskStatus.ToDo, TaskPriority.Low),
            ct);
        secondTaskResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var secondTaskId = await secondTaskResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);

        // Soft-delete Project -> IsDeleted=true in DB, project returns 404, and its tasks disappear from GET /api/v1/tasks
        (await adminClient.DeleteAsync($"/api/v1/projects/{projectId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await adminClient.GetAsync($"/api/v1/projects/{projectId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var globalTasksAfterProjectDelete = await adminClient.GetFromJsonAsync<PagedResult<TaskSummaryDto>>("/api/v1/tasks", JsonOptions, ct);
        globalTasksAfterProjectDelete!.Items.ShouldNotContain(t => t.Id == secondTaskId);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var softDeletedTask = await db.Set<ProjectTask>().IgnoreQueryFilters().SingleAsync(t => t.Id == taskId, ct);
            softDeletedTask.IsDeleted.ShouldBeTrue();
            softDeletedTask.DeletedAt.ShouldNotBeNull();

            var softDeletedProject = await db.Set<Project>().IgnoreQueryFilters().SingleAsync(p => p.Id == projectId, ct);
            softDeletedProject.IsDeleted.ShouldBeTrue();
            softDeletedProject.DeletedAt.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task UpdateMyPasswordShouldKeepTheCallingSessionAndEndAllOthers()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        using var factory = fixture.CreateFactory($"crm_password_{Guid.NewGuid():N}");
        var anonClient = factory.CreateClient();

        (await anonClient.PostAsJsonAsync(
                "/api/v1/users",
                new CreateUserCommand("Pass", "Owner", null, "password-owner@ngo.org", "StrongPass1!", "StrongPass1!"),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var callingSession = await LoginAsync(anonClient, "password-owner@ngo.org", "StrongPass1!", ct);
        var otherSession = await LoginAsync(anonClient, "password-owner@ngo.org", "StrongPass1!", ct);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", callingSession.AccessToken);
        var changeResp = await client.PostAsJsonAsync(
            "/api/v1/users/me/password",
            new UpdatePasswordCommand("StrongPass1!", "NewStrongPass2!"),
            ct);
        changeResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var renewed = (await changeResp.Content.ReadFromJsonAsync<AuthTokensDto>(JsonOptions, ct))!;

        // The calling session stays signed in with the renewed pair...
        (await anonClient.PostAsJsonAsync(
                "/api/v1/auth/refresh-token",
                new RefreshTokensCommand(renewed.AccessToken, renewed.RefreshToken),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // ...while every refresh token issued before the change is revoked, the caller's old one included.
        (await anonClient.PostAsJsonAsync(
                "/api/v1/auth/refresh-token",
                new RefreshTokensCommand(callingSession.AccessToken, callingSession.RefreshToken),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonClient.PostAsJsonAsync(
                "/api/v1/auth/refresh-token",
                new RefreshTokensCommand(otherSession.AccessToken, otherSession.RefreshToken),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Only the new password signs in.
        (await anonClient.PostAsJsonAsync("/api/v1/auth/login", new AuthenticateUserCommand("password-owner@ngo.org", "StrongPass1!"), ct))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await LoginAsync(anonClient, "password-owner@ngo.org", "NewStrongPass2!", ct);
    }

    [Fact]
    public async Task DeletingATopicShouldPurgeItsSoftDeletedTasksButNotWhileItHasLiveOnes()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        using var factory = fixture.CreateFactory($"crm_topic_delete_{Guid.NewGuid():N}");
        var (client, projectId) = await CreateAdminWithProjectAsync(factory, "topics-owner@ngo.org", ct);
        var backlogTopicId = await CreateTopicAsync(client, projectId, "Backlog", 0, ct);
        var doneTopicId = await CreateTopicAsync(client, projectId, "Done", 1, ct);

        // Deleting a task only soft-deletes it, so the task still references its topic...
        var doneTaskId = await CreateTaskAsync(client, projectId, doneTopicId, "Finished", ct);
        (await client.DeleteAsync($"/api/v1/projects/{projectId}/tasks/{doneTaskId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // ...but it no longer blocks deleting the topic: the topic goes, and the soft-deleted task with it.
        (await client.DeleteAsync($"/api/v1/projects/{projectId}/topics/{doneTopicId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Set<ProjectTask>().IgnoreQueryFilters().AnyAsync(t => t.Id == doneTaskId, ct)).ShouldBeFalse();
        }

        // A topic with a live task still cannot be deleted, and the task stays.
        var openTaskId = await CreateTaskAsync(client, projectId, backlogTopicId, "Open", ct);
        (await client.DeleteAsync($"/api/v1/projects/{projectId}/topics/{backlogTopicId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.GetAsync($"/api/v1/projects/{projectId}/tasks/{openTaskId}", ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<AuthTokensDto> LoginAsync(HttpClient client, string email, string password, CancellationToken ct)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/auth/login", new AuthenticateUserCommand(email, password), ct);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await resp.Content.ReadFromJsonAsync<AuthTokensDto>(JsonOptions, ct))!;
    }

    private static async Task<(HttpClient Client, Guid ProjectId)> CreateAdminWithProjectAsync(
        WebApplicationFactory<Program> factory,
        string email,
        CancellationToken ct)
    {
        var anonClient = factory.CreateClient();
        (await anonClient.PostAsJsonAsync(
                "/api/v1/users",
                new CreateUserCommand("Project", "Owner", null, email, "StrongPass1!", "StrongPass1!"),
                ct))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        var tokens = await LoginAsync(anonClient, email, "StrongPass1!", ct);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        (await client.PostAsJsonAsync("/api/v1/ngo", new CreateNgoCommand("Test NGO"), ct))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var projectResp = await client.PostAsJsonAsync(
            "/api/v1/projects",
            new CreateProjectCommand("Test Project", "Desc", ProjectVisibility.Private, new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc)),
            ct);
        projectResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (client, await projectResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct));
    }

    private static async Task<Guid> CreateTopicAsync(HttpClient client, Guid projectId, string title, byte order, CancellationToken ct)
    {
        var resp = await client.PostAsJsonAsync($"/api/v1/projects/{projectId}/topics", new CreateTopicCommand(title, order), ct);
        resp.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await resp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);
    }

    private static async Task<Guid> CreateTaskAsync(HttpClient client, Guid projectId, Guid topicId, string title, CancellationToken ct)
    {
        var resp = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks",
            new CreateTaskCommand(topicId, title, null, null, null, new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc)),
            ct);
        resp.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await resp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);
    }

    private static MultipartFormDataContent CreateMultipartFile(
        string fieldName,
        string fileName,
        string contentType,
        byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, fieldName, fileName);
        return content;
    }
}
