using Crm.Application.Common.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Dtos.Task.Queries;

/// <summary>
/// Represents a query to retrieve all file attachments for a specific task.
/// </summary>
/// <param name="ProjectId">The unique identifier of the parent project.</param>
/// <param name="TaskId">The unique identifier of the task.</param>
public record GetTaskAttachmentsQuery(
    Guid ProjectId,
    Guid TaskId) : IRequest<List<TaskAttachmentDto>>, ITaskScoped
{
    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.View;
}
