using System.Net.Mime;
using Asp.Versioning;
using Crm.Api.Consts;
using Crm.Api.Extensions;
using Crm.Api.Security;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task;
using Crm.Application.Dtos.Task.Commands;
using Crm.Application.Dtos.Task.Queries;
using Crm.Domain.Common;
using Crm.Domain.Enums;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

/// <summary>
/// Controller responsible for managing project tasks (Planner, Board, Tables, global My Tasks),
/// task assignments, tags, comments, unread comment tracking, and file attachments.
/// </summary>
/// <param name="mediator">The mediator instance used for dispatching commands and queries.</param>
[ApiController]
[ApiVersion(ApiVersions.V1)]
[Route(ApiRouteConstants.ProjectTasksRoutePrefix)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class ProjectTasksController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Retrieves a paginated, filtered, and sorted list of project tasks
    /// (supports <c>filter=mine</c> for personal tasks, tag/status/priority/date filters, text search, and null-safe date sorting).
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="query">Query parameters for filtering (e.g., <c>filter=mine</c>), sorting, and pagination.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated list of task summary DTOs, including unread comment and attachment counts.</returns>
    [HttpGet]
    [HasAccessRight(AccessRight.ViewTask)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<TaskSummaryDto>>> GetTasks(
        Guid projectId,
        [FromQuery] GetProjectTasksQuery query,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query with { ProjectId = projectId }, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves detailed information about a specific task within a project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The detailed task DTO including attachments, external links, and metadata.</returns>
    [HttpGet("{taskId:guid}", Name = nameof(GetTaskById))]
    [HasAccessRight(AccessRight.ViewTask)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDto>> GetTaskById(
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetTaskByIdQuery(projectId, taskId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new task within a project. Automatically populates <c>EndDate</c> if created with <c>Completed</c> status and null <c>EndDate</c>.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="command">The task creation payload.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The unique identifier of the newly created task.</returns>
    [HttpPost]
    [HasAccessRight(AccessRight.CreateTask)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Guid>> CreateTask(
        Guid projectId,
        [FromBody] CreateTaskCommand command,
        CancellationToken cancellationToken)
    {
        var taskId = await mediator.Send(command with { ProjectId = projectId }, cancellationToken);
        return CreatedAtRoute(nameof(GetTaskById), new { projectId, taskId }, taskId);
    }

    /// <summary>
    /// Updates an existing task within a project. Automatically populates <c>EndDate</c> when transitioning to <c>Completed</c> status if <c>EndDate</c> is null.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="taskId">The unique identifier of the task to update.</param>
    /// <param name="command">The updated task payload.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful update.</returns>
    [HttpPut("{taskId:guid}")]
    [HasAccessRight(AccessRight.UpdateTask)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTask(
        Guid projectId,
        Guid taskId,
        [FromBody] UpdateTaskCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command with { ProjectId = projectId, TaskId = taskId }, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Partially updates a task's status, board topic/section, or completion date.
    /// Automatically populates <c>EndDate</c> with the current UTC timestamp if status becomes <c>Completed</c> and <c>EndDate</c> is null.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="command">The updated status, topic ID, or end date.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful update.</returns>
    [HttpPatch("{taskId:guid}/status")]
    [HasAccessRight(AccessRight.UpdateTask)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTaskStatus(
        Guid projectId,
        Guid taskId,
        [FromBody] UpdateTaskStatusCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command with { ProjectId = projectId, TaskId = taskId }, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Deletes a task and removes all associated file attachments from storage.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="taskId">The unique identifier of the task to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful deletion.</returns>
    [HttpDelete("{taskId:guid}")]
    [HasAccessRight(AccessRight.DeleteTask)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTask(
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteTaskCommand(projectId, taskId), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Assigns a user to a task.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="userId">The unique identifier of the user to assign.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful assignment.</returns>
    [HttpPost("{taskId:guid}/assignees/{userId:guid}")]
    [HasAccessRight(AccessRight.UpdateTask)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignUser(
        Guid projectId,
        Guid taskId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new AssignUserToTaskCommand(projectId, taskId, userId), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Removes a user assignment from a task.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="userId">The unique identifier of the user to unassign.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful removal.</returns>
    [HttpDelete("{taskId:guid}/assignees/{userId:guid}")]
    [HasAccessRight(AccessRight.UpdateTask)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnassignUser(
        Guid projectId,
        Guid taskId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new UnassignUserFromTaskCommand(projectId, taskId, userId), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Attaches a project-scoped tag to a task.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="tagId">The unique identifier of the project tag.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful tag association.</returns>
    [HttpPost("{taskId:guid}/tags/{tagId:guid}")]
    [HasAccessRight(AccessRight.UpdateTask)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddTag(
        Guid projectId,
        Guid taskId,
        Guid tagId,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new AddTagToTaskCommand(projectId, taskId, tagId), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Detaches a project-scoped tag from a task.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="tagId">The unique identifier of the project tag to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful removal.</returns>
    [HttpDelete("{taskId:guid}/tags/{tagId:guid}")]
    [HasAccessRight(AccessRight.UpdateTask)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTag(
        Guid projectId,
        Guid taskId,
        Guid tagId,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new RemoveTagFromTaskCommand(projectId, taskId, tagId), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Retrieves a paginated list of comments on a task, ordered chronologically.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="query">Pagination query parameters.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated chronological list of task comment DTOs.</returns>
    [HttpGet("{taskId:guid}/comments")]
    [HasAccessRight(AccessRight.ViewTask)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<TaskCommentDto>>> GetComments(
        Guid projectId,
        Guid taskId,
        [FromQuery] GetTaskCommentsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query with { ProjectId = projectId, TaskId = taskId }, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Posts a new comment on a task.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="command">The comment content payload.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The unique identifier of the newly created comment.</returns>
    [HttpPost("{taskId:guid}/comments")]
    [HasAccessRight(AccessRight.ViewTask)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Guid>> CreateComment(
        Guid projectId,
        Guid taskId,
        [FromBody] CreateTaskCommentCommand command,
        CancellationToken cancellationToken)
    {
        var commentId = await mediator.Send(command with { ProjectId = projectId, TaskId = taskId }, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, commentId);
    }

    /// <summary>
    /// Updates an existing comment on a task.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="commentId">The unique identifier of the comment to update.</param>
    /// <param name="command">The updated comment content payload.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful update.</returns>
    [HttpPut("{taskId:guid}/comments/{commentId:guid}")]
    [HasAccessRight(AccessRight.ViewTask)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateComment(
        Guid projectId,
        Guid taskId,
        Guid commentId,
        [FromBody] UpdateTaskCommentCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command with { ProjectId = projectId, TaskId = taskId, CommentId = commentId }, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Deletes a comment from a task.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="commentId">The unique identifier of the comment to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful deletion.</returns>
    [HttpDelete("{taskId:guid}/comments/{commentId:guid}")]
    [HasAccessRight(AccessRight.ViewTask)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteComment(
        Guid projectId,
        Guid taskId,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteTaskCommentCommand(projectId, taskId, commentId), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Marks all comments on a task as read for the currently authenticated user.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating the read status was updated.</returns>
    [HttpPost("{taskId:guid}/comments/read")]
    [HasAccessRight(AccessRight.ViewTask)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkCommentsAsRead(
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new MarkTaskCommentsReadCommand(projectId, taskId), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Retrieves metadata and download URLs for all files attached to a task.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of task attachment DTOs.</returns>
    [HttpGet("{taskId:guid}/attachments")]
    [HasAccessRight(AccessRight.ViewTask)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<TaskAttachmentDto>>> GetAttachments(
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetTaskAttachmentsQuery(projectId, taskId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Uploads and attaches a file to a task using the Docker volume file storage system.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="file">The file to upload and attach.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The created task attachment DTO with its download URL.</returns>
    [HttpPost("{taskId:guid}/attachments")]
    [HasAccessRight(AccessRight.UpdateTask)]
    [Consumes(MediaTypeNames.Multipart.FormData)]
    [RequestSizeLimit(FileLimitConstants.MaxDocumentMultipartRequestSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = FileLimitConstants.MaxDocumentMultipartRequestSize)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<TaskAttachmentDto>> UploadAttachment(
        Guid projectId,
        Guid taskId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        using var stream = file.OpenReadStream();

        var command = new UploadTaskAttachmentCommand(
            projectId,
            taskId,
            stream,
            file.FileName,
            file.ContentType,
            file.Length);

        var attachment = await mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, attachment);
    }

    /// <summary>
    /// Downloads/retrieves the file stream of a specific task attachment.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="attachmentId">The unique identifier of the attachment.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The file stream of the requested task attachment.</returns>
    [HttpGet(ApiRouteConstants.TaskAttachmentFileRouteTemplate)]
    [HasAccessRight(AccessRight.ViewTask)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachment(
        Guid projectId,
        Guid taskId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var fileDto = await mediator.Send(new GetTaskAttachmentFileQuery(projectId, taskId, attachmentId), cancellationToken);
        Response.Headers.XContentTypeOptions = SecurityHeaderValues.NoSniff;
        return File(fileDto.Stream, fileDto.GetContentType(), fileDto.FileName, enableRangeProcessing: true);
    }

    /// <summary>
    /// Deletes a file attachment from a task and removes the stored file from disk.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="attachmentId">The unique identifier of the attachment to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful deletion.</returns>
    [HttpDelete("{taskId:guid}/attachments/{attachmentId:guid}")]
    [HasAccessRight(AccessRight.UpdateTask)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAttachment(
        Guid projectId,
        Guid taskId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteTaskAttachmentCommand(projectId, taskId, attachmentId), cancellationToken);
        return NoContent();
    }
}
