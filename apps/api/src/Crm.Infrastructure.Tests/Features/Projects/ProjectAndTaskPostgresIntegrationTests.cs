using System.Text.Json.Nodes;
using Crm.Application.Dtos.Project;
using Crm.Application.Dtos.Task;
using Crm.Application.Features.Projects.Mappings;
using Crm.Application.Features.Tasks.Mappings;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Repositories;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Crm.Infrastructure.Tests.Features.Projects;

/// <summary>
/// Executes PostgreSQL integration tests verifying EF Core migrations, composite <see cref="ProjectMember"/> PKs,
/// case-insensitive tag uniqueness, JSONB <c>ExternalLinks</c>/<c>Metadata</c>, <see cref="TaskReadState"/> upserts,
/// atomic join inserts, SQL-projected project/task DTO queries, and cleanup of inaccessible user data.
/// </summary>
/// <param name="fixture">The shared PostgreSQL Testcontainers fixture.</param>
public class ProjectAndTaskPostgresIntegrationTests(
    ProjectAndTaskPostgresIntegrationTests.PostgresFixture fixture)
    : IClassFixture<ProjectAndTaskPostgresIntegrationTests.PostgresFixture>
{
    static ProjectAndTaskPostgresIntegrationTests()
    {
        new ProjectMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
        new TaskMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
    }

    [Fact]
    public async Task ProjectMemberCompositePrimaryKeyAndCaseInsensitiveTagUniquenessShouldBeEnforcedInPostgres()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        await using var setupContext = fixture.CreateDbContext();

        var (authUser, user) = CreateTestUser("Ivan", "Franko");
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Title = "Composite Key Check",
            Visibility = ProjectVisibility.Private,
            StartDate = DateTime.UtcNow,
            CreatedBy = user.Id,
        };
        setupContext.AddRange(authUser, project);
        await setupContext.SaveChangesAsync(ct);

        await using var ctx1 = fixture.CreateDbContext();
        ctx1.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = user.Id,
            RoleTitle = "Admin",
            Claim = ProjectClaim.Manage,
        });
        ctx1.Add(new Tag { ProjectId = project.Id, Title = "Urgent" });
        await ctx1.SaveChangesAsync(ct);

        await using var ctx2 = fixture.CreateDbContext();
        ctx2.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = user.Id,
            RoleTitle = "Duplicate",
            Claim = ProjectClaim.View,
        });

        await Should.ThrowAsync<DbUpdateException>(async () => await ctx2.SaveChangesAsync(ct));

        // Case-insensitive tag uniqueness enforced at DB level via NormalizedTitle unique index (B-10)
        await using var ctx3 = fixture.CreateDbContext();
        var tagRepo = new TagRepository(ctx3);
        (await tagRepo.ExistsWithTitleAsync(project.Id, "  URGENT  ", cancellationToken: ct)).ShouldBeTrue();

        ctx3.Add(new Tag { ProjectId = project.Id, Title = "urgent" });
        var tagDupEx = await Should.ThrowAsync<DbUpdateException>(async () => await ctx3.SaveChangesAsync(ct));
        tagDupEx.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);

        // Topic order uniqueness enforced at DB level via (ProjectId, Order) unique index
        await using var ctx4 = fixture.CreateDbContext();
        ctx4.Add(new Topic { ProjectId = project.Id, Title = "Topic One", Order = 0 });
        await ctx4.SaveChangesAsync(ct);

        await using var ctx5 = fixture.CreateDbContext();
        var topicRepo = new TopicRepository(ctx5);
        (await topicRepo.ExistsWithOrderAsync(project.Id, 0, cancellationToken: ct)).ShouldBeTrue();

        ctx5.Add(new Topic { ProjectId = project.Id, Title = "Topic Two", Order = 0 });
        var topicOrderDupEx = await Should.ThrowAsync<DbUpdateException>(async () => await ctx5.SaveChangesAsync(ct));
        topicOrderDupEx.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task TaskReadStateUnreadMarkersDeleteOnReadExternalLinksMetadataAndSummaryDtoShouldWorkInPostgres()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var (authUserA, userA) = CreateTestUser("Olena", "Pchilka");
        var (authUserB, userB) = CreateTestUser("Lesya", "Ukrainka");
        var taskStartDate = new DateTime(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
        var t1 = new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 11, 16, 11, 0, 0, DateTimeKind.Utc);
        var t3 = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Title = "Task Tracker Project",
            Visibility = ProjectVisibility.Private,
            StartDate = taskStartDate,
            CreatedBy = userA.Id,
            Members =
            [
                new ProjectMember
                {
                    UserId = userB.Id,
                    RoleTitle = "Розробник",
                    Claim = ProjectClaim.Edit,
                },
            ],
            Topics =
            [
                new Topic { Title = "Розробка", Order = 0 },
                new Topic { Title = "SMM", Order = 1 },
            ],
            Tags =
            [
                new Tag { Title = "API" },
            ],
        };

        await using var setupContext = fixture.CreateDbContext();
        setupContext.AddRange(authUserA, authUserB, project);
        await setupContext.SaveChangesAsync(ct);

        var topic = project.Topics.First(t => t.Title == "Розробка");
        var tag = project.Tags.Single();

        var task = new ProjectTask
        {
            ProjectId = project.Id,
            TopicId = topic.Id,
            Title = "Implement Planner",
            Description = "Details in **Markdown**",
            Status = ProjectTaskStatus.Done,
            Priority = TaskPriority.High,
            StartDate = taskStartDate,
            EndDate = null,
            ExternalLinks =
            [
                new TaskExternalLink { Title = "Spec", Url = "https://example.org/spec" },
            ],
            Metadata = new JsonObject { ["grantId"] = "GR-42", ["hours"] = 12 },
            CreatedAt = t1,
            UpdatedAt = t1,
            AssignedUsers = [userB],
            Tags = [tag],
            Attachments =
            [
                new TaskAttachment
                {
                    UploadedBy = userA.Id,
                    OriginalFileName = "spec.pdf",
                    StoredFileName = "stored-spec.pdf",
                    ContentType = "application/pdf",
                    FileSize = 100,
                    CreatedAt = t1,
                },
            ],
            Comments =
            [
                new TaskComment
                {
                    UserId = userA.Id,
                    Content = "Initial comment",
                    CreatedAt = t1,
                    UpdatedAt = t1,
                },
                new TaskComment
                {
                    UserId = userA.Id,
                    Content = "Follow-up comment",
                    CreatedAt = t2,
                    UpdatedAt = t2,
                },
                new TaskComment
                {
                    UserId = userB.Id,
                    Content = "Self comment (not unread for userB)",
                    CreatedAt = t2.AddMinutes(1),
                    UpdatedAt = t2.AddMinutes(1),
                },
            ],
        };

        setupContext.Add(task);
        await setupContext.SaveChangesAsync(ct);

        // Mark unread for userA comments (userB's own comment creates no marker for userB)
        await using var notifyContext = fixture.CreateDbContext();
        var notifyCommentRepo = new TaskCommentRepository(notifyContext);
        foreach (var authorComment in task.Comments.Where(c => c.UserId == userA.Id))
        {
            await notifyCommentRepo.MarkUnreadForOtherUsersAsync(authorComment, userA.Id, ct);
        }

        await notifyContext.SaveChangesAsync(ct);

        // Before marking read: userB sees 2 unread comments (both by userA, excluding userB's own comment)
        await using var assertBeforeReadContext = fixture.CreateDbContext();
        var preReadTaskRepo = new ProjectTaskRepository(assertBeforeReadContext);
        var unreadBeforeRead = await preReadTaskRepo.GetUnreadCommentCountsAsync([task.Id], userB.Id, ct);
        unreadBeforeRead[task.Id].ShouldBe(2);
        (await preReadTaskRepo.GetUnreadCommentCountsAsync([task.Id], userA.Id, ct)).ShouldBeEmpty();

        // Mark as read -> TaskReadState rows are deleted from DB; unread count becomes 0
        await using var actContext = fixture.CreateDbContext();
        var commentRepo = new TaskCommentRepository(actContext);
        await commentRepo.MarkAsReadAsync(task.Id, userB.Id, ct);

        await using var afterReadContext = fixture.CreateDbContext();
        var afterReadTaskRepo = new ProjectTaskRepository(afterReadContext);
        (await afterReadTaskRepo.GetUnreadCommentCountsAsync([task.Id], userB.Id, ct)).ShouldBeEmpty();
        (await afterReadContext.Set<TaskReadState>().CountAsync(rs => rs.Comment.TaskId == task.Id && rs.UserId == userB.Id, ct)).ShouldBe(0);

        // New comment arrives at t3 > t2 -> creates TaskReadState for this comment; unread count becomes 1
        var newComment = new TaskComment
        {
            TaskId = task.Id,
            UserId = userA.Id,
            Content = "New comment after read",
            CreatedAt = t3,
            UpdatedAt = t3,
        };
        var commentRepo2 = new TaskCommentRepository(afterReadContext);
        await commentRepo2.CreateAsync(newComment, ct);
        await commentRepo2.MarkUnreadForOtherUsersAsync(newComment, userA.Id, ct);
        await afterReadContext.SaveChangesAsync(ct);

        (await afterReadTaskRepo.GetUnreadCommentCountsAsync([task.Id], userB.Id, ct))[task.Id].ShouldBe(1);

        // Second MarkAsReadAsync deletes TaskReadState again cleanly so nothing persists after read
        await commentRepo2.MarkAsReadAsync(task.Id, userB.Id, ct);
        (await afterReadTaskRepo.GetUnreadCommentCountsAsync([task.Id], userB.Id, ct)).ShouldBeEmpty();
        (await afterReadContext.Set<TaskReadState>().CountAsync(rs => rs.Comment.TaskId == task.Id && rs.UserId == userB.Id, ct)).ShouldBe(0);

        // Verify TaskDto & TaskSummaryDto SQL projections (including JSONB ExternalLinks, Metadata, AttachmentsCount)
        await using var assertContext = fixture.CreateDbContext();
        var projectRepo = new ProjectRepository(assertContext);
        var loadedProject = await projectRepo.GetAsync<ProjectDto>(project.Id, ct);
        loadedProject.Members.Single().Claim.ShouldBe(ProjectClaim.Edit);
        loadedProject.Members.Single().RoleTitle.ShouldBe("Розробник");

        var assertTaskRepo = new ProjectTaskRepository(assertContext);
        var loadedTask = await assertTaskRepo.GetByProjectAndIdAsync<TaskDto>(project.Id, task.Id, ct);
        loadedTask.Status.ShouldBe(ProjectTaskStatus.Done);
        loadedTask.StartDate.ShouldBe(taskStartDate);
        loadedTask.EndDate.ShouldBeNull();
        loadedTask.AssignedUsers.ShouldHaveSingleItem().Id.ShouldBe(userB.Id);
        loadedTask.Tags.ShouldHaveSingleItem().Title.ShouldBe("API");
        loadedTask.ExternalLinks.ShouldHaveSingleItem().Url.ShouldBe("https://example.org/spec");
        loadedTask.Metadata["grantId"]?.GetValue<string>().ShouldBe("GR-42");

        var summaryPage = await assertTaskRepo.GetPagedAsync<TaskSummaryDto>(
            new ProjectTaskFilter(ProjectId: null, IsMine: true),
            userB.Id,
            cancellationToken: ct);
        var summaryItem = summaryPage.Items.ShouldHaveSingleItem();
        summaryItem.Id.ShouldBe(task.Id);
        summaryItem.AttachmentsCount.ShouldBe(1);
    }

    [Fact]
    public async Task DeletingUserShouldSetProjectCommentAttachmentCreatorsNullAndCascadeJoinAndReadStateRowsInPostgres()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var (authUser, user) = CreateTestUser("Deleted", "Creator");
        var topic = new Topic { Id = Guid.NewGuid(), Title = "Розробка", Order = 0 };
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Title = "Surviving Project",
            Visibility = ProjectVisibility.Public,
            StartDate = DateTime.UtcNow,
            CreatedBy = user.Id,
            Topics = [topic],
            Members =
            [
                new ProjectMember { UserId = user.Id, RoleTitle = "Owner", Claim = ProjectClaim.Manage },
            ],
        };

        await using var setupContext = fixture.CreateDbContext();
        setupContext.AddRange(authUser, project);
        await setupContext.SaveChangesAsync(ct);

        var task = new ProjectTask
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            TopicId = topic.Id,
            Title = "Surviving Task",
            StartDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            AssignedUsers = [user],
            Comments =
            [
                new TaskComment
                {
                    UserId = user.Id,
                    Content = "Surviving Comment",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    ReadStates =
                    [
                        new TaskReadState
                        {
                            UserId = user.Id,
                        },
                    ],
                },
            ],
            Attachments =
            [
                new TaskAttachment
                {
                    UploadedBy = user.Id,
                    OriginalFileName = "doc.pdf",
                    StoredFileName = "stored.pdf",
                    ContentType = "application/pdf",
                    FileSize = 10,
                    CreatedAt = DateTime.UtcNow,
                },
            ],
        };

        setupContext.Add(task);
        await setupContext.SaveChangesAsync(ct);

        await using var deleteContext = fixture.CreateDbContext();
        await deleteContext.Set<User>().Where(u => u.Id == user.Id).ExecuteDeleteAsync(ct);

        await using var assertContext = fixture.CreateDbContext();
        var survivingProject = await assertContext.Set<Project>().SingleAsync(p => p.Id == project.Id, ct);
        var survivingTask = await assertContext.Set<ProjectTask>()
            .Include(t => t.AssignedUsers)
            .Include(t => t.Comments)
            .ThenInclude(c => c.ReadStates)
            .Include(t => t.Attachments)
            .SingleAsync(t => t.Id == task.Id, ct);

        survivingProject.CreatedBy.ShouldBeNull();
        survivingTask.AssignedUsers.ShouldBeEmpty();
        survivingTask.Comments.Single().UserId.ShouldBeNull();
        survivingTask.Comments.Single().ReadStates.ShouldBeEmpty();
        survivingTask.Attachments.Single().UploadedBy.ShouldBeNull();
        (await assertContext.Set<ProjectMember>().CountAsync(m => m.ProjectId == project.Id, ct)).ShouldBe(0);

        // Deleting the whole project must succeed in PostgreSQL even with Restrict FK between Topic and Task
        await using var deleteProjectContext = fixture.CreateDbContext();
        var projectRepo = new ProjectRepository(deleteProjectContext);
        await projectRepo.DeleteAsync(project.Id, ct);

        await using var afterProjectDeleteContext = fixture.CreateDbContext();
        (await afterProjectDeleteContext.Set<Project>().AnyAsync(p => p.Id == project.Id, ct)).ShouldBeFalse();
        (await afterProjectDeleteContext.Set<ProjectTask>().AnyAsync(t => t.Id == task.Id, ct)).ShouldBeFalse();
        (await afterProjectDeleteContext.Set<Topic>().AnyAsync(t => t.Id == topic.Id, ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task AtomicJoinInsertsRemoveInaccessibleUserDataAndManagerCheckShouldWorkInPostgres()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var (authCreator, creator) = CreateTestUser("Project", "Creator");
        var (authMember, member) = CreateTestUser("Project", "Member");
        var (authOutsider, outsider) = CreateTestUser("Outside", "User");
        var (authInactive, inactive) = CreateTestUser("Inactive", "Member");
        inactive.IsActive = false;

        var topic = new Topic { Id = Guid.NewGuid(), Title = "Board", Order = 0 };
        var existingTag = new Tag { Id = Guid.NewGuid(), Title = "Existing" };
        var extraTag = new Tag { Id = Guid.NewGuid(), Title = "Extra" };
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Title = "Private Assignee Project",
            Visibility = ProjectVisibility.Private,
            StartDate = DateTime.UtcNow,
            CreatedBy = creator.Id,
            Topics = [topic],
            Tags = [existingTag, extraTag],
            Members =
            [
                new ProjectMember { UserId = member.Id, Claim = ProjectClaim.Edit },
                new ProjectMember { UserId = inactive.Id, Claim = ProjectClaim.View },
            ],
        };

        var task = new ProjectTask
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            TopicId = topic.Id,
            Title = "Verify Atomic Joins & Cleanup",
            StartDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Tags = [existingTag],
        };

        await using var setupContext = fixture.CreateDbContext();
        setupContext.AddRange(authCreator, authMember, authOutsider, authInactive, project, task);
        await setupContext.SaveChangesAsync(ct);

        await using var actContext = fixture.CreateDbContext();
        var userRepo = new UserRepository(actContext);
        var projectRepo = new ProjectRepository(actContext);
        var tagRepo = new TagRepository(actContext);
        var taskRepo = new ProjectTaskRepository(actContext);
        var commentRepo = new TaskCommentRepository(actContext);
        var utcNow = DateTime.UtcNow;

        // Manager protection check
        (await projectRepo.HasAnotherManagerAsync(project.Id, creator.Id, ct)).ShouldBeFalse();
        (await projectRepo.HasAnotherManagerAsync(project.Id, member.Id, ct)).ShouldBeTrue();

        // Idempotent EF Core join inserts: first returns true (persisted on SaveChanges), second returns false
        (await taskRepo.AssignToTaskAsync(task.Id, creator.Id, ct)).ShouldBeTrue();
        await actContext.SaveChangesAsync(ct);
        (await taskRepo.AssignToTaskAsync(task.Id, creator.Id, ct)).ShouldBeFalse();
        (await taskRepo.AssignToTaskAsync(task.Id, outsider.Id, ct)).ShouldBeTrue();
        (await tagRepo.AddToTaskAsync(task.Id, extraTag.Id, ct)).ShouldBeTrue();
        await actContext.SaveChangesAsync(ct);
        (await tagRepo.AddToTaskAsync(task.Id, extraTag.Id, ct)).ShouldBeFalse();
        (await tagRepo.RemoveFromTaskAsync(task.Id, existingTag.Id, ct)).ShouldBeTrue();
        (await tagRepo.RemoveFromTaskAsync(task.Id, existingTag.Id, ct)).ShouldBeFalse();

        // Seed TaskReadState for both creator and outsider, then clean up inaccessible user data (B-13)
        var cleanupComment = new TaskComment
        {
            TaskId = task.Id,
            UserId = member.Id,
            Content = "Comment for cleanup test",
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            ReadStates =
            [
                new TaskReadState { UserId = creator.Id },
                new TaskReadState { UserId = outsider.Id },
            ],
        };
        await commentRepo.CreateAsync(cleanupComment, ct);
        await actContext.SaveChangesAsync(ct);
        await taskRepo.RemoveInaccessibleUserDataAsync(project.Id, ct);

        await using var assertContext = fixture.CreateDbContext();
        var updatedTask = await assertContext.Set<ProjectTask>()
            .Include(t => t.AssignedUsers)
            .Include(t => t.Tags)
            .Include(t => t.Comments)
            .ThenInclude(c => c.ReadStates)
            .SingleAsync(t => t.Id == task.Id, ct);

        updatedTask.AssignedUsers.Select(u => u.Id).ShouldBe([creator.Id]);
        updatedTask.Comments.Single().ReadStates.Select(rs => rs.UserId).ShouldBe([creator.Id]);
        updatedTask.Tags.Select(t => t.Id).ShouldBe([extraTag.Id]);

        await Should.ThrowAsync<EntitiesNotFoundException>(
            () => tagRepo.EnsureAllExistInProjectAsync(project.Id, [existingTag.Id, Guid.NewGuid()], ct));
    }

    private static (AuthUser AuthUser, User User) CreateTestUser(string firstName, string lastName)
    {
        var userId = Guid.NewGuid();
        var email = $"{firstName.ToLowerInvariant()}.{userId:N}@ngo.org";
        var user = new User
        {
            Id = userId,
            FirstName = firstName,
            LastName = lastName,
            CreatedAt = DateTime.UtcNow,
        };
        var authUser = new AuthUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserProfile = user,
        };
        return (authUser, user);
    }

    public sealed class PostgresFixture : IAsyncLifetime
    {
        private PostgreSqlContainer? _container;

        public bool IsDockerAvailable { get; private set; }

        public DbContextOptions<ApplicationDbContext> DbContextOptions { get; private set; }

        public ApplicationDbContext CreateDbContext() => new(DbContextOptions);

        public async ValueTask InitializeAsync()
        {
            try
            {
                _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
                await _container.StartAsync();

                DbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseNpgsql(_container.GetConnectionString())
                    .Options;

                await using var context = CreateDbContext();
                await context.Database.MigrateAsync();
                IsDockerAvailable = true;
            }
            catch (Exception)
            {
                IsDockerAvailable = false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_container is not null)
            {
                await _container.DisposeAsync();
            }
        }
    }
}
