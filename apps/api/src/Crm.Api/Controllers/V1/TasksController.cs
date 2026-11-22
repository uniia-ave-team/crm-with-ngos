using Asp.Versioning;
using Crm.Api.Consts;
using Crm.Api.Security;
using Crm.Application.Dtos.Tasks;
using Crm.Application.Features.Tasks.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Enums;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

/// <summary>
/// Controller providing cross-project task listings (such as the global "My Tasks" view)
/// filtered strictly by the authenticated user's visible projects.
/// </summary>
/// <param name="mediator">The mediator instance used for dispatching queries.</param>
/// <param name="currentUserService">The service for retrieving the currently authenticated user's identifier.</param>
[ApiController]
[ApiVersion(ApiVersions.V1)]
[Route(ApiRouteConstants.DefaultControllerRoute)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class TasksController(
    IMediator mediator,
    ICurrentUserService currentUserService) : ControllerBase
{
    /// <summary>
    /// Retrieves a paginated, filtered, and sorted list of tasks across all projects visible to the currently authenticated user
    /// (supports <c>filter=mine</c> for global personal tasks, tag/status/priority/date filters, text search, and null-safe date sorting).
    /// </summary>
    /// <param name="query">Query parameters for filtering (e.g., <c>filter=mine</c>), sorting, and pagination.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated list of task summary DTOs, including unread comment and attachment counts.</returns>
    [HttpGet]
    [HasAccessRight(AccessRight.ViewTask)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<TaskSummaryDto>>> GetTasks(
        [FromQuery] GetProjectTasksQuery query,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            query with { ProjectId = null, CurrentUserId = currentUserService.GetUserId() },
            cancellationToken);
        return Ok(result);
    }
}
