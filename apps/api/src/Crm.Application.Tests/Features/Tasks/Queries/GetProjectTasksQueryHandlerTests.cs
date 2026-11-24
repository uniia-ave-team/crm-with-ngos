using Crm.Application.Dtos.Tasks;
using Crm.Application.Features.Tasks.Mappings;
using Crm.Application.Features.Tasks.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.Tasks.Queries;

/// <summary>
/// Unit tests for <see cref="GetProjectTasksQueryHandler"/> verifying paginated <see cref="TaskSummaryDto"/> projections,
/// unread comment count hydration, URL enrichment, and global cross-project querying.
/// </summary>
public class GetProjectTasksQueryHandlerTests
{
    private readonly IProjectTaskRepository _taskRepository = Substitute.For<IProjectTaskRepository>();
    private readonly IFileUrlProvider _fileUrlProvider = Substitute.For<IFileUrlProvider>();
    private readonly GetProjectTasksQueryHandler _handler;

    static GetProjectTasksQueryHandlerTests()
    {
        new TaskMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
    }

    public GetProjectTasksQueryHandlerTests()
    {
        _handler = new GetProjectTasksQueryHandler(
            _taskRepository,
            _fileUrlProvider,
            Substitute.For<ILogger<GetProjectTasksQueryHandler>>());
    }

    [Fact]
    public async Task HandlePagedQueryShouldHydrateUnreadCountsEnrichUrlsAndPreservePaginationMetadata()
    {
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var devTopicId = Guid.NewGuid();
        var smmTopicId = Guid.NewGuid();
        var smmTaskId = Guid.NewGuid();
        var devTaskId = Guid.NewGuid();

        _fileUrlProvider.GetFileUrl("other-avatar.png", $"api/v1/users/{otherUserId}/avatar")
            .Returns($"https://cdn.example/api/v1/users/{otherUserId}/avatar");
        _fileUrlProvider.GetFileUrl("me-avatar.png", "api/v1/users/me/avatar")
            .Returns("https://cdn.example/api/v1/users/me/avatar");

        _taskRepository.GetPagedAsync<TaskSummaryDto>(
                Arg.Is<ProjectTaskFilter>(filter => filter.ProjectId == projectId && !filter.IsMine),
                currentUserId,
                null,
                SortOrderConstants.Ascending,
                2,
                5,
                Arg.Any<CancellationToken>())
            .Returns(new PagedResult<TaskSummaryDto>(
            [
                new TaskSummaryDto(
                    devTaskId,
                    projectId,
                    devTopicId,
                    "Розробка",
                    "Setup DB",
                    null,
                    ProjectTaskStatus.ToDo,
                    TaskPriority.High,
                    DateTime.UtcNow,
                    null,
                    EndDateWasAuto: false,
                    UnreadCommentsCount: 0,
                    AttachmentsCount: 2,
                    [new TaskAssigneeDto(currentUserId, "Ivan", "Franko", null, "me-avatar.png")],
                    [],
                    DateTime.UtcNow,
                    DateTime.UtcNow),
                new TaskSummaryDto(
                    smmTaskId,
                    projectId,
                    smmTopicId,
                    "SMM",
                    "Instagram Post",
                    null,
                    ProjectTaskStatus.Done,
                    TaskPriority.Medium,
                    DateTime.UtcNow,
                    null,
                    EndDateWasAuto: false,
                    UnreadCommentsCount: 0,
                    AttachmentsCount: 0,
                    [new TaskAssigneeDto(otherUserId, "Olena", "Kovalenko", null, "other-avatar.png")],
                    [],
                    DateTime.UtcNow,
                    DateTime.UtcNow),
            ],
            totalCount: 12,
            pageNumber: 2,
            pageSize: 5));

        _taskRepository.GetUnreadCommentCountsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                currentUserId,
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, int> { [smmTaskId] = 2 });

        var result = await _handler.Handle(
            new GetProjectTasksQuery(PageNumber: 2, PageSize: 5) { ProjectId = projectId, CurrentUserId = currentUserId },
            CancellationToken.None);

        result.TotalCount.ShouldBe(12);
        result.PageNumber.ShouldBe(2);
        result.PageSize.ShouldBe(5);
        result.Items.Count.ShouldBe(2);

        var items = result.Items.ToList();
        items[0].Title.ShouldBe("Setup DB");
        items[0].AttachmentsCount.ShouldBe(2);
        items[0].UnreadCommentsCount.ShouldBe(0);
        items[0].AssignedUsers.Single().AvatarUrl.ShouldBe("https://cdn.example/api/v1/users/me/avatar");

        items[1].Title.ShouldBe("Instagram Post");
        items[1].AttachmentsCount.ShouldBe(0);
        items[1].UnreadCommentsCount.ShouldBe(2);
        items[1].AssignedUsers.Single().AvatarUrl.ShouldBe($"https://cdn.example/api/v1/users/{otherUserId}/avatar");
    }

    [Fact]
    public async Task HandleGlobalMineQueryShouldPassNullProjectIdAndIsMineTrueToRepository()
    {
        var currentUserId = Guid.NewGuid();

        _taskRepository.GetPagedAsync<TaskSummaryDto>(
                Arg.Is<ProjectTaskFilter>(filter => filter.ProjectId == null && filter.IsMine),
                currentUserId,
                null,
                SortOrderConstants.Ascending,
                1,
                10,
                Arg.Any<CancellationToken>())
            .Returns(new PagedResult<TaskSummaryDto>([], 0, 1, 10));

        _taskRepository.GetUnreadCommentCountsAsync(
                Arg.Any<IEnumerable<Guid>>(),
                currentUserId,
                Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _handler.Handle(
            new GetProjectTasksQuery(Filter: "mine") { ProjectId = null, CurrentUserId = currentUserId },
            CancellationToken.None);

        result.TotalCount.ShouldBe(0);
        await _taskRepository.Received(1).GetPagedAsync<TaskSummaryDto>(
            Arg.Is<ProjectTaskFilter>(filter => filter.ProjectId == null && filter.IsMine),
            currentUserId,
            null,
            SortOrderConstants.Ascending,
            1,
            10,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTaskCommentsQueryHandlerShouldReturnPaginatedCommentsWithEnrichedAvatars()
    {
        var currentUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var commentRepository = Substitute.For<ITaskCommentRepository>();

        _fileUrlProvider.GetFileUrl("me-avatar.png", "api/v1/users/me/avatar")
            .Returns("https://cdn.example/api/v1/users/me/avatar");

        commentRepository.GetPagedByTaskIdAsync<TaskCommentDto>(taskId, 2, 5, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<TaskCommentDto>(
            [
                new TaskCommentDto(
                    Guid.NewGuid(),
                    taskId,
                    currentUserId,
                    "Ivan",
                    "Franko",
                    null,
                    "me-avatar.png",
                    "Page 2 comment",
                    DateTime.UtcNow,
                    DateTime.UtcNow),
            ],
            totalCount: 6,
            pageNumber: 2,
            pageSize: 5));

        var handler = new GetTaskCommentsQueryHandler(
            commentRepository,
            _fileUrlProvider,
            Substitute.For<ILogger<GetTaskCommentsQueryHandler>>());

        var result = await handler.Handle(
            new GetTaskCommentsQuery(PageNumber: 2, PageSize: 5) { ProjectId = projectId, TaskId = taskId, CurrentUserId = currentUserId },
            CancellationToken.None);

        result.TotalCount.ShouldBe(6);
        result.PageNumber.ShouldBe(2);
        result.PageSize.ShouldBe(5);
        result.Items.ShouldHaveSingleItem().AuthorAvatarUrl.ShouldBe("https://cdn.example/api/v1/users/me/avatar");
    }
}
