using System.Linq.Expressions;
using Crm.Application.Dtos.Project;
using Crm.Application.Dtos.Task;
using Crm.Application.Dtos.Task.Queries;
using Crm.Application.Features.Tasks.Mappings;
using Crm.Domain.Common;
using Crm.Domain.Consts;
using Crm.Domain.Consts.Entities.GeneralConstants;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Infrastructure.Helpers;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Repositories;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Crm.Infrastructure.Tests.Features.Projects;

/// <summary>
/// Verifies PostgreSQL SQL query translation (via Npgsql <c>ToQueryString</c> without requiring Docker)
/// for Mapster projections, native <see cref="GenericRepository{T}.ApplyOrdering{TResult}"/> sorting,
/// null-safe dates, semantic enum sorting, <see cref="TaskReadState"/> unread counts, JSONB fields,
/// global cross-project visibility filters, and task filters.
/// </summary>
public class ProjectAndTaskQueryTests
{
    static ProjectAndTaskQueryTests()
    {
        TypeAdapterConfig.GlobalSettings.Scan(typeof(TaskMappingRegister).Assembly);
    }

    [Fact]
    public void NpgsqlProjectionsAndNativeTaskOrderingShouldTranslateToSqlWithoutClientEvaluation()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=crm_translation_check;Username=postgres;Password=postgres")
            .Options;

        using var context = new ApplicationDbContext(options);
        var repository = new TestProjectTaskRepository(context);
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        string projectDetailSql = context.Set<Project>()
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .ProjectToType<ProjectDto>()
            .ToQueryString();
        projectDetailSql.ShouldContain("ProjectMember", Case.Insensitive);
        projectDetailSql.ShouldContain("Topic", Case.Insensitive);
        projectDetailSql.ShouldContain("Tag", Case.Insensitive);
        CountOccurrences(projectDetailSql, "\"ProjectMember\"").ShouldBe(1);
        CountOccurrences(projectDetailSql, "\"Topic\"").ShouldBe(1);
        CountOccurrences(projectDetailSql, "\"Tag\"").ShouldBe(1);

        var otherUserId = Guid.NewGuid();
        var projectRepo = new TestProjectRepository(context);
        string accessSqlUser1 = projectRepo.BuildAccessSql(projectId, userId);
        string accessSqlUser2 = projectRepo.BuildAccessSql(projectId, otherUserId);

        accessSqlUser1.ShouldContain("ProjectMember", Case.Insensitive);
        accessSqlUser1.ShouldContain(userId.ToString());
        accessSqlUser2.ShouldContain(otherUserId.ToString());
        accessSqlUser2.ShouldNotContain(userId.ToString());

        string visibleSql = context.Set<Project>()
            .AsNoTracking()
            .Where(TestProjectRepository.Filter(userId, "crm"))
            .OrderByDescending(p => p.Title)
            .ProjectToType<ProjectSummaryDto>()
            .ToQueryString();
        visibleSql.ShouldContain("Project", Case.Insensitive);
        visibleSql.ShouldContain("StartDate");

        string membersSql = context.Set<ProjectMember>()
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId)
            .OrderBy(m => m.User.LastName)
            .ThenBy(m => m.User.FirstName)
            .ProjectToType<ProjectMemberDto>()
            .ToQueryString();
        membersSql.ShouldContain("Claim");
        membersSql.ShouldContain("FirstName");
        membersSql.ShouldContain("LastName");
        membersSql.ShouldContain("Patronymic");

        string topicsSql = context.Set<Topic>()
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .Where(QueryFilterExpressions.ContainsIgnoreCase<Topic>(t => t.Title, "dev"))
            .OrderBy(t => t.Order)
            .ProjectToType<TopicDto>()
            .ToQueryString();
        topicsSql.ShouldContain("ORDER BY");

        string tagsSql = context.Set<Tag>()
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .Where(QueryFilterExpressions.ContainsIgnoreCase<Tag>(t => t.Title, "urg"))
            .OrderBy(t => t.Title)
            .ProjectToType<TagDto>()
            .ToQueryString();
        tagsSql.ShouldContain("ORDER BY");

        MapsterSortResolver.GetSortMapping<Project, ProjectSummaryDto>().ShouldContainKey(nameof(ProjectSummaryDto.Title));
        MapsterSortResolver.GetSortMapping<Tag, TagDto>().ShouldContainKey(nameof(TagDto.Title));
        MapsterSortResolver.GetSortMapping<Topic, TopicDto>().ShouldContainKey(nameof(TopicDto.Order));

        string commentsSql = context.Set<TaskComment>()
            .AsNoTracking()
            .Where(c => c.TaskId == taskId)
            .OrderBy(c => c.CreatedAt)
            .ProjectToType<TaskCommentDto>()
            .ToQueryString();
        commentsSql.ShouldContain("ORDER BY");

        string attachmentsSql = context.Set<TaskAttachment>()
            .AsNoTracking()
            .Where(a => a.TaskId == taskId)
            .OrderByDescending(a => a.CreatedAt)
            .ProjectToType<TaskAttachmentDto>()
            .ToQueryString();
        attachmentsSql.ShouldContain("ORDER BY");

        // Task detail DTO projection including ExternalLinks and Metadata jsonb columns
        string taskDetailSql = context.Set<ProjectTask>()
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId && t.Id == taskId)
            .ProjectToType<TaskDto>()
            .ToQueryString();
        taskDetailSql.ShouldContain("ExternalLinks");
        taskDetailSql.ShouldContain("Metadata");
        taskDetailSql.ShouldContain("TaskAttachment", Case.Insensitive);
        CountOccurrences(taskDetailSql, "\"TaskAttachment\"").ShouldBe(1);
        CountOccurrences(taskDetailSql, "\"TaskUser\"").ShouldBe(1);
        CountOccurrences(taskDetailSql, "\"TaskTag\"").ShouldBe(1);

        // Task summary DTO projection uses SQL COUNT(*) for AttachmentsCount instead of fetching attachment rows
        string taskSummarySql = context.Set<ProjectTask>()
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .ProjectToType<TaskSummaryDto>()
            .ToQueryString();
        taskSummarySql.ShouldContain("count(", Case.Insensitive);
        taskSummarySql.ShouldNotContain("StoredFileName");
        taskSummarySql.ShouldNotContain("OriginalFileName");
        CountOccurrences(taskSummarySql, "\"TaskUser\"").ShouldBe(1);
        CountOccurrences(taskSummarySql, "\"TaskTag\"").ShouldBe(1);

        // Unread comment count query backed by TaskReadState
        string unreadSql = context.Set<TaskReadState>()
            .AsNoTracking()
            .Where(rs => rs.UserId == userId && rs.Comment.TaskId == taskId)
            .GroupBy(rs => rs.Comment.TaskId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToQueryString();
        unreadSql.ShouldContain("TaskReadState");
        unreadSql.ShouldContain("GROUP BY");

        // Global cross-project My Tasks query (ProjectId == null) composes ProjectVisibilityExpressions in SQL
        string globalMineSql = repository.BuildTaskSummaryQuery(
            new GetProjectTasksQuery(Filter: TaskConstants.MineFilterValue) { ProjectId = null },
            userId).ToQueryString();
        globalMineSql.ShouldContain("ProjectMember", Case.Insensitive);
        globalMineSql.ShouldContain("Visibility");
        globalMineSql.ShouldContain("TaskReadState");

        string tagTasksSql = context.Set<Tag>()
            .Where(t => t.Id == taskId)
            .SelectMany(t => t.Tasks)
            .ToQueryString();
        tagTasksSql.ShouldContain("TaskTag");

        string[] sortFields =
        [
            string.Empty,
            nameof(TaskSummaryDto.StartDate),
            nameof(TaskSummaryDto.EndDate),
            nameof(TaskSummaryDto.Priority),
            nameof(TaskSummaryDto.Status),
            nameof(TaskSummaryDto.UnreadCommentsCount),
            nameof(TaskSummaryDto.Title),
            nameof(TaskSummaryDto.TopicTitle),
            nameof(TaskSummaryDto.CreatedAt),
        ];
        foreach (string sortField in sortFields)
        {
            foreach (string direction in new[] { SortOrderConstants.Ascending, SortOrderConstants.Descending })
            {
                var taskQuery = new GetProjectTasksQuery(
                    Filter: "mine",
                    TagIds: [Guid.NewGuid()],
                    SearchTerm: "api",
                    OrderBy: sortField,
                    SortOrder: direction)
                {
                    ProjectId = projectId,
                };

                string sql = repository.BuildTaskSummaryQuery(taskQuery, userId).ToQueryString();
                sql.ShouldContain("ORDER BY");
                sql.ShouldContain("ILIKE");
            }
        }

        string unreadSqlSort = repository.BuildTaskSummaryQuery(
            new GetProjectTasksQuery(OrderBy: nameof(TaskSummaryDto.UnreadCommentsCount), SortOrder: SortOrderConstants.Descending)
            {
                ProjectId = projectId,
            },
            userId).ToQueryString();
        unreadSqlSort.ShouldContain("count(", Case.Insensitive);
        unreadSqlSort.ShouldContain("TaskReadState");

        string topicTitleSqlSort = repository.BuildTaskSummaryQuery(
            new GetProjectTasksQuery(OrderBy: nameof(TaskSummaryDto.TopicTitle), SortOrder: SortOrderConstants.Ascending)
            {
                ProjectId = projectId,
            },
            userId).ToQueryString();
        MapsterSortResolver.GetSortMapping<ProjectTask, TaskSummaryDto>()[nameof(TaskSummaryDto.TopicTitle)].ShouldBe("Topic.Title");
        topicTitleSqlSort.Split("ORDER BY").Last().ShouldContain("\"Title\"");
    }

    [Fact]
    public void OrderByNullableDatesInBothDirectionsShouldAlwaysPlaceNullDatesLast()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=crm_translation_check;Username=postgres;Password=postgres")
            .Options;

        using var context = new ApplicationDbContext(options);
        var repository = new TestProjectTaskRepository(context);
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var start = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var nullEndTask = CreateTask(projectId, "No End Date", [userId], start, null);
        var earlyTask = CreateTask(projectId, "Early", [userId], start, new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc));
        var lateTask = CreateTask(projectId, "Late", [userId], start, new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc));
        var source = new[] { nullEndTask, lateTask, earlyTask }.AsQueryable();

        var ascQuery = new GetProjectTasksQuery(OrderBy: "endDate", SortOrder: SortOrderConstants.Ascending) { ProjectId = projectId };
        var ascResult = repository.SortInMemory(source, ascQuery, userId).Select(t => t.Title).ToList();
        ascResult.ShouldBe(["Early", "Late", "No End Date"]);

        var descQuery = new GetProjectTasksQuery(OrderBy: "endDate", SortOrder: SortOrderConstants.Descending) { ProjectId = projectId };
        var descResult = repository.SortInMemory(source, descQuery, userId).Select(t => t.Title).ToList();
        descResult.ShouldBe(["Late", "Early", "No End Date"]);
    }

    [Fact]
    public void OrderByPriorityShouldSortSemanticallyAndFallbackToNativeSortingForTitle()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=crm_translation_check;Username=postgres;Password=postgres")
            .Options;

        using var context = new ApplicationDbContext(options);
        var repository = new TestProjectTaskRepository(context);
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var start = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        var low = CreateTask(projectId, "Bravo", [], start, null, priority: TaskPriority.Low);
        var critical = CreateTask(projectId, "Alpha", [], start, null, priority: TaskPriority.Critical);
        var medium = CreateTask(projectId, "Delta", [], start, null, priority: TaskPriority.Medium);
        var high = CreateTask(projectId, "Charlie", [], start, null, priority: TaskPriority.High);
        var source = new[] { low, medium, high, critical }.AsQueryable();

        var priorityQuery = new GetProjectTasksQuery(OrderBy: "priority", SortOrder: SortOrderConstants.Descending) { ProjectId = projectId };
        var priorityResult = repository.SortInMemory(source, priorityQuery, userId).Select(t => t.Title).ToList();
        priorityResult.ShouldBe(["Alpha", "Charlie", "Delta", "Bravo"]);

        var titleQuery = new GetProjectTasksQuery(OrderBy: "title", SortOrder: SortOrderConstants.Ascending) { ProjectId = projectId };
        var titleResult = repository.SortInMemory(source, titleQuery, userId).Select(t => t.Title).ToList();
        titleResult.ShouldBe(["Alpha", "Bravo", "Charlie", "Delta"]);
    }

    [Fact]
    public void FilterTasksShouldFilterByTagPriorityStatusMineAndGlobalProjectVisibility()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var visibleProjectId = Guid.NewGuid();
        var hiddenProjectId = Guid.NewGuid();
        var backendTagId = Guid.NewGuid();
        var smmTagId = Guid.NewGuid();
        var start = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        var visibleProject = new Project
        {
            Id = visibleProjectId,
            Title = "Visible",
            Visibility = ProjectVisibility.Public,
            CreatedBy = otherUserId,
        };
        var hiddenProject = new Project
        {
            Id = hiddenProjectId,
            Title = "Hidden Private",
            Visibility = ProjectVisibility.Private,
            CreatedBy = otherUserId,
        };

        var match = CreateTask(visibleProjectId, "API Endpoint", [userId], start, null, ProjectTaskStatus.InProgress, TaskPriority.Critical, [backendTagId], project: visibleProject);
        var hiddenTask = CreateTask(hiddenProjectId, "Secret Task", [userId], start, null, ProjectTaskStatus.InProgress, TaskPriority.Critical, [backendTagId], project: hiddenProject);
        var wrongUser = CreateTask(visibleProjectId, "Other User Task", [otherUserId], start, null, ProjectTaskStatus.InProgress, TaskPriority.Critical, [backendTagId], project: visibleProject);
        var wrongTag = CreateTask(visibleProjectId, "Social Post", [userId], start, null, ProjectTaskStatus.InProgress, TaskPriority.Critical, [smmTagId], project: visibleProject);
        var wrongPriority = CreateTask(visibleProjectId, "Refactor", [userId], start, null, ProjectTaskStatus.InProgress, TaskPriority.Low, [backendTagId], project: visibleProject);

        var globalMineQuery = new GetProjectTasksQuery(
            Filter: TaskConstants.MineFilterValue,
            Status: ProjectTaskStatus.InProgress,
            Priority: TaskPriority.Critical,
            TagIds: [backendTagId])
        {
            ProjectId = null,
        };

        var result = TestProjectTaskRepository.FilterInMemory(
                new[] { hiddenTask, wrongUser, wrongTag, wrongPriority, match }.AsQueryable(),
                globalMineQuery,
                userId)
            .Select(t => t.Title)
            .ToList();

        result.ShouldHaveSingleItem().ShouldBe("API Endpoint");
    }

    [Fact]
    public void ProjectTagAndTopicRepositoryFiltersShouldTranslateVisibilityAndILikeToSql()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=crm_translation_check;Username=postgres;Password=postgres")
            .Options;

        using var context = new ApplicationDbContext(options);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var visiblePrivateMember = new Project
        {
            Id = projectId,
            Title = "Ukraine Relief Fund",
            Description = "Humanitarian aid",
            Visibility = ProjectVisibility.Private,
            CreatedBy = otherUserId,
            Members = [new ProjectMember { ProjectId = projectId, UserId = userId, Claim = ProjectClaim.View }],
        };
        var hiddenPrivate = new Project
        {
            Id = Guid.NewGuid(),
            Title = "Secret Aid",
            Visibility = ProjectVisibility.Private,
            CreatedBy = otherUserId,
        };
        var publicVisible = new Project
        {
            Id = Guid.NewGuid(),
            Title = "IT Operations",
            Visibility = ProjectVisibility.Public,
            CreatedBy = otherUserId,
        };

        var hiddenPrivateForExCreator = new Project
        {
            Id = Guid.NewGuid(),
            Title = "Removed Creator Private",
            Visibility = ProjectVisibility.Private,
            CreatedBy = userId,
            Members = [],
        };

        var visibilityPredicate = TestProjectRepository.Filter(userId, null).Compile();
        visibilityPredicate(visiblePrivateMember).ShouldBeTrue();
        visibilityPredicate(hiddenPrivate).ShouldBeFalse();
        visibilityPredicate(hiddenPrivateForExCreator).ShouldBeFalse();
        visibilityPredicate(publicVisible).ShouldBeTrue();

        string projectSearchSql = context.Set<Project>()
            .AsNoTracking()
            .Where(TestProjectRepository.Filter(userId, "100% aid_fund"))
            .ToQueryString();
        projectSearchSql.ShouldContain("ILIKE");
        projectSearchSql.ShouldContain("100\\% aid\\_fund");

        string tagSearchSql = context.Set<Tag>()
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .Where(QueryFilterExpressions.ContainsIgnoreCase<Tag>(t => t.Title, "urg"))
            .ToQueryString();
        tagSearchSql.ShouldContain("ILIKE");
        tagSearchSql.ShouldContain("ProjectId");

        string topicSearchSql = context.Set<Topic>()
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .Where(QueryFilterExpressions.ContainsIgnoreCase<Topic>(t => t.Title, "smm"))
            .ToQueryString();
        topicSearchSql.ShouldContain("ILIKE");
        topicSearchSql.ShouldContain("ProjectId");
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.OrdinalIgnoreCase)) != -1)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }

    private static ProjectTask CreateTask(
        Guid projectId,
        string title,
        IEnumerable<Guid> assigneeIds,
        DateTime startDate,
        DateTime? endDate,
        ProjectTaskStatus status = ProjectTaskStatus.ToDo,
        TaskPriority priority = TaskPriority.Medium,
        IEnumerable<Guid>? tagIds = null,
        Guid? topicId = null,
        Project? project = null)
    {
        var taskId = Guid.NewGuid();
        return new ProjectTask
        {
            Id = taskId,
            ProjectId = projectId,
            Project = project!,
            TopicId = topicId ?? Guid.NewGuid(),
            Title = title,
            Status = status,
            Priority = priority,
            StartDate = startDate,
            EndDate = endDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            AssignedUsers = [.. assigneeIds.Select(uid => new User { Id = uid })],
            Tags = [.. (tagIds ?? []).Select(tid => new Tag { Id = tid, ProjectId = projectId })],
            Comments = [],
        };
    }

    private sealed class TestProjectTaskRepository(ApplicationDbContext context) : ProjectTaskRepository(context)
    {
        public static IQueryable<ProjectTask> FilterInMemory(IQueryable<ProjectTask> source, GetProjectTasksQuery taskQuery, Guid currentUserId)
            => ApplyTaskFilters(source, taskQuery.Adapt<ProjectTaskFilter>(), currentUserId);

        public IQueryable<TaskSummaryDto> BuildTaskSummaryQuery(GetProjectTasksQuery taskQuery, Guid currentUserId)
        {
            var filter = taskQuery.Adapt<ProjectTaskFilter>();
            return ApplyTaskOrdering<TaskSummaryDto>(
                    ApplyTaskFilters(DbSet.AsNoTracking(), filter, currentUserId),
                    currentUserId,
                    filter.IsMine,
                    taskQuery.OrderBy,
                    taskQuery.SortOrder)
                .ProjectToType<TaskSummaryDto>();
        }

        public IQueryable<ProjectTask> SortInMemory(IQueryable<ProjectTask> source, GetProjectTasksQuery taskQuery, Guid currentUserId)
        {
            var filter = taskQuery.Adapt<ProjectTaskFilter>();
            return ApplyTaskOrdering<TaskSummaryDto>(
                ApplyTaskFilters(source, filter, currentUserId),
                currentUserId,
                filter.IsMine,
                taskQuery.OrderBy,
                taskQuery.SortOrder);
        }
    }

    private sealed class TestProjectRepository(ApplicationDbContext context) : ProjectRepository(context)
    {
        public static Expression<Func<Project, bool>> Filter(Guid userId, string? searchTerm)
            => BuildVisibleProjectFilter(userId, searchTerm);

        public string BuildAccessSql(Guid projectId, Guid userId)
            => BuildAccessQuery(projectId, userId).ToQueryString();
    }
}
