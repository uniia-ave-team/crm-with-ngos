using Crm.Domain.Enums;

namespace Crm.Domain.Common;

/// <summary>
/// Represents repository filter criteria for querying project tasks.
/// </summary>
/// <param name="ProjectId">The optional unique identifier of the parent project; <c>null</c> queries across all visible projects.</param>
/// <param name="IsMine">Whether to restrict results to tasks assigned to the current user.</param>
/// <param name="TopicId">An optional board topic/section identifier filter.</param>
/// <param name="Status">An optional task execution status filter.</param>
/// <param name="Priority">An optional task priority level filter.</param>
/// <param name="TagIds">An optional collection of tag identifiers to match against task tags.</param>
/// <param name="StartDateFrom">An optional lower bound (inclusive) for the task start date.</param>
/// <param name="StartDateTo">An optional upper bound (inclusive) for the task start date.</param>
/// <param name="EndDateFrom">An optional lower bound (inclusive) for the task end date.</param>
/// <param name="EndDateTo">An optional upper bound (inclusive) for the task end date.</param>
/// <param name="SearchTerm">An optional search term matched against task title and description.</param>
public record ProjectTaskFilter(
    Guid? ProjectId = null,
    bool IsMine = false,
    Guid? TopicId = null,
    ProjectTaskStatus? Status = null,
    TaskPriority? Priority = null,
    IReadOnlyList<Guid>? TagIds = null,
    DateTime? StartDateFrom = null,
    DateTime? StartDateTo = null,
    DateTime? EndDateFrom = null,
    DateTime? EndDateTo = null,
    string? SearchTerm = null);
