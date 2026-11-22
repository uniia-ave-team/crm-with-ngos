using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Tasks;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Features.Tasks.Queries;

/// <summary>
/// Represents a query to retrieve all file attachments for a specific task.
/// </summary>
/// <param name="ProjectId">The unique identifier of the parent project.</param>
/// <param name="TaskId">The unique identifier of the task.</param>
public record GetTaskAttachmentsQuery(
    Guid ProjectId,
    Guid TaskId) : IQuery<List<TaskAttachmentDto>>, ITaskScoped
{
    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.View;
}
