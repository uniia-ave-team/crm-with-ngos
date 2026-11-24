using Asp.Versioning;
using Crm.Api.Consts;
using Crm.Api.Security;
using Crm.Application.Dtos.Projects;
using Crm.Application.Features.Projects.Commands;
using Crm.Application.Features.Projects.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Enums;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

/// <summary>
/// Controller responsible for managing projects, project-level user roles/claims, board topics, and project-scoped tags.
/// </summary>
/// <param name="mediator">The mediator instance used for dispatching commands and queries.</param>
/// <param name="currentUserService">The service for retrieving the currently authenticated user's identifier.</param>
[ApiController]
[ApiVersion(ApiVersions.V1)]
[Route(ApiRouteConstants.DefaultControllerRoute)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class ProjectsController(
    IMediator mediator,
    ICurrentUserService currentUserService) : ControllerBase
{
    /// <summary>
    /// Retrieves a paginated, filtered, and sorted list of projects visible to the currently authenticated user.
    /// </summary>
    /// <param name="query">Pagination, sorting, and search query parameters.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated list of project summary DTOs.</returns>
    [HttpGet]
    [HasAccessRight(AccessRight.ViewProject)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ProjectSummaryDto>>> GetProjects(
        [FromQuery] GetProjectsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            query with { CurrentUserId = currentUserService.GetUserId() },
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves detailed information about a specific project by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the project.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The detailed project DTO including members, topics, and tags.</returns>
    [HttpGet("{id:guid}", Name = nameof(GetProjectById))]
    [HasAccessRight(AccessRight.ViewProject)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDto>> GetProjectById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetProjectByIdQuery(id, currentUserService.GetUserId()),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new project and assigns the creator as a project manager.
    /// </summary>
    /// <param name="command">The command containing project title, description, visibility, and date range settings.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The unique identifier of the newly created project.</returns>
    [HttpPost]
    [HasAccessRight(AccessRight.CreateProject)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> CreateProject(
        [FromBody] CreateProjectCommand command,
        CancellationToken cancellationToken)
    {
        var id = await mediator.Send(
            command with { CurrentUserId = currentUserService.GetUserId() },
            cancellationToken);
        return CreatedAtRoute(nameof(GetProjectById), new { id }, id);
    }

    /// <summary>
    /// Updates an existing project's details, visibility, and date range.
    /// </summary>
    /// <param name="id">The unique identifier of the project to update.</param>
    /// <param name="command">The updated project details, visibility, and date range settings.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful update.</returns>
    [HttpPut("{id:guid}")]
    [HasAccessRight(AccessRight.UpdateProject)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProject(
        Guid id,
        [FromBody] UpdateProjectCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(
            command with { Id = id, CurrentUserId = currentUserService.GetUserId() },
            cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Deletes a project and all its associated tasks, topics, tags, and member assignments.
    /// </summary>
    /// <param name="id">The unique identifier of the project to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful deletion.</returns>
    [HttpDelete("{id:guid}")]
    [HasAccessRight(AccessRight.DeleteProject)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProject(
        Guid id,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteProjectCommand(id, currentUserService.GetUserId()), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Retrieves all members assigned to a specific project, including their functional role titles and project claims.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of project member DTOs.</returns>
    [HttpGet("{projectId:guid}/members")]
    [HasAccessRight(AccessRight.ViewProject)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ProjectMemberDto>>> GetMembers(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetProjectMembersQuery(projectId, currentUserService.GetUserId()),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Assigns an active user to a project with a custom functional role title and project-level permission claim.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="command">The user ID, custom role title, and project claim.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating the project member assignment was created.</returns>
    [HttpPost("{projectId:guid}/members")]
    [HasAccessRight(AccessRight.UpdateProject)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignMember(
        Guid projectId,
        [FromBody] AssignProjectMemberCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command with { ProjectId = projectId }, cancellationToken);
        return StatusCode(StatusCodes.Status201Created);
    }

    /// <summary>
    /// Updates an existing project member's custom functional role title and project-level permission claim.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="userId">The unique identifier of the user on the project.</param>
    /// <param name="command">The updated role title and project claim.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful update.</returns>
    [HttpPut("{projectId:guid}/members/{userId:guid}")]
    [HasAccessRight(AccessRight.UpdateProject)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMember(
        Guid projectId,
        Guid userId,
        [FromBody] UpdateProjectMemberCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command with { ProjectId = projectId, UserId = userId }, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Removes a user from a project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="userId">The unique identifier of the user to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful removal.</returns>
    [HttpDelete("{projectId:guid}/members/{userId:guid}")]
    [HasAccessRight(AccessRight.UpdateProject)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new RemoveProjectMemberCommand(projectId, userId), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Retrieves a paginated, filtered, and sorted list of board topics/sections for a project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="query">Pagination, sorting, and search query parameters.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated list of topic DTOs.</returns>
    [HttpGet("{projectId:guid}/topics")]
    [HasAccessRight(AccessRight.ViewProject)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<TopicDto>>> GetTopics(
        Guid projectId,
        [FromQuery] GetProjectTopicsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query with { ProjectId = projectId }, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new board topic/section within a project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="command">The topic title and display order.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The unique identifier of the created topic.</returns>
    [HttpPost("{projectId:guid}/topics")]
    [HasAccessRight(AccessRight.UpdateProject)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Guid>> CreateTopic(
        Guid projectId,
        [FromBody] CreateTopicCommand command,
        CancellationToken cancellationToken)
    {
        var topicId = await mediator.Send(command with { ProjectId = projectId }, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, topicId);
    }

    /// <summary>
    /// Updates an existing board topic/section within a project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="topicId">The unique identifier of the topic to update.</param>
    /// <param name="command">The updated topic title and display order.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful update.</returns>
    [HttpPut("{projectId:guid}/topics/{topicId:guid}")]
    [HasAccessRight(AccessRight.UpdateProject)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateTopic(
        Guid projectId,
        Guid topicId,
        [FromBody] UpdateTopicCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command with { ProjectId = projectId, TopicId = topicId }, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Deletes a board topic/section from a project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="topicId">The unique identifier of the topic to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful deletion.</returns>
    [HttpDelete("{projectId:guid}/topics/{topicId:guid}")]
    [HasAccessRight(AccessRight.UpdateProject)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteTopic(
        Guid projectId,
        Guid topicId,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteTopicCommand(projectId, topicId), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Retrieves a paginated, filtered, and sorted list of tags strictly scoped to a specific project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="query">Pagination, sorting, and search query parameters.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated list of project-scoped tag DTOs.</returns>
    [HttpGet("{projectId:guid}/tags")]
    [HasAccessRight(AccessRight.ViewProject)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<TagDto>>> GetTags(
        Guid projectId,
        [FromQuery] GetProjectTagsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query with { ProjectId = projectId }, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new project-scoped tag.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="command">The tag title.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The unique identifier of the created tag.</returns>
    [HttpPost("{projectId:guid}/tags")]
    [HasAccessRight(AccessRight.UpdateProject)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Guid>> CreateTag(
        Guid projectId,
        [FromBody] CreateTagCommand command,
        CancellationToken cancellationToken)
    {
        var tagId = await mediator.Send(command with { ProjectId = projectId }, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, tagId);
    }

    /// <summary>
    /// Updates an existing project-scoped tag's title.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="tagId">The unique identifier of the tag to update.</param>
    /// <param name="command">The updated tag title.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful update.</returns>
    [HttpPut("{projectId:guid}/tags/{tagId:guid}")]
    [HasAccessRight(AccessRight.UpdateProject)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateTag(
        Guid projectId,
        Guid tagId,
        [FromBody] UpdateTagCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command with { ProjectId = projectId, TagId = tagId }, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Deletes a project-scoped tag.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="tagId">The unique identifier of the tag to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful deletion.</returns>
    [HttpDelete("{projectId:guid}/tags/{tagId:guid}")]
    [HasAccessRight(AccessRight.UpdateProject)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTag(
        Guid projectId,
        Guid tagId,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteTagCommand(projectId, tagId), cancellationToken);
        return NoContent();
    }
}
