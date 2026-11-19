using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Project.Commands;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Projects.Commands;

/// <summary>
/// Handles the <see cref="UpdateProjectMemberCommand"/> to update a project member's role title and project claim,
/// preventing demotion of the last remaining active project manager.
/// </summary>
/// <param name="projectRepository">The repository for retrieving and updating project member records.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class UpdateProjectMemberCommandHandler(
    IProjectRepository projectRepository,
    ILogger<UpdateProjectMemberCommandHandler> logger) : IRequestHandler<UpdateProjectMemberCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(UpdateProjectMemberCommand request, CancellationToken cancellationToken)
    {
        LogUpdatingProjectMember(logger, request.UserId, request.ProjectId);

        var member = await projectRepository.GetMemberForUpdateAsync(request.ProjectId, request.UserId, cancellationToken);

        if (member.Claim >= ProjectClaim.Manage
            && request.Claim < ProjectClaim.Manage
            && !await projectRepository.HasAnotherManagerAsync(request.ProjectId, request.UserId, cancellationToken))
        {
            throw new CannotRemoveLastProjectManagerException(request.ProjectId);
        }

        request.Adapt(member);

        LogProjectMemberUpdated(logger, request.UserId, request.ProjectId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingProjectMember, Level = LogLevel.Information, Message = "Updating project member user ID: {UserId} on project ID: {ProjectId}")]
    private static partial void LogUpdatingProjectMember(ILogger logger, Guid userId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.ProjectMemberUpdated, Level = LogLevel.Information, Message = "Project member user ID: {UserId} updated on project ID: {ProjectId}")]
    private static partial void LogProjectMemberUpdated(ILogger logger, Guid userId, Guid projectId);
}
