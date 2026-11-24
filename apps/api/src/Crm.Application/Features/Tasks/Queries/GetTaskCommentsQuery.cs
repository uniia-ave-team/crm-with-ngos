using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Tasks;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Features.Tasks.Queries;

/// <summary>
/// Represents a paginated query to retrieve comments on a specific task in chronological order.
/// </summary>
/// <param name="PageNumber">The 1-based page number to retrieve.</param>
/// <param name="PageSize">The maximum number of comments per page.</param>
public record GetTaskCommentsQuery(
    int PageNumber = PaginationConstants.MinPageNumber,
    int PageSize = PaginationConstants.DefaultPageSize) : IQuery<PagedResult<TaskCommentDto>>, ITaskScoped
{
    /// <summary>
    /// Gets the unique identifier of the parent project, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid ProjectId { get; init; }

    /// <summary>
    /// Gets the unique identifier of the task, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid TaskId { get; init; }

    /// <summary>
    /// Gets the unique identifier of the currently authenticated user, populated by the controller.
    /// </summary>
    [JsonIgnore]
    public Guid CurrentUserId { get; init; }

    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.View;
}
