using Crm.Application.Common.Consts;
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
public sealed partial class UpdateProjectMemberCommandHandler(
    IProjectRepository projectRepository,
    ILogger<UpdateProjectMemberCommandHandler> logger) : ICommandHandler<UpdateProjectMemberCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(UpdateProjectMemberCommand command, CancellationToken cancellationToken)
    {
        LogUpdatingProjectMember(logger, command.UserId, command.ProjectId);

        await projectRepository.LockForUpdateAsync(command.ProjectId, cancellationToken);

        var member = await projectRepository.GetMemberForUpdateAsync(command.ProjectId, command.UserId, cancellationToken);

        if (member.Claim >= ProjectClaim.Manage
            && command.Claim < ProjectClaim.Manage
            && !await projectRepository.HasAnotherManagerAsync(command.ProjectId, command.UserId, cancellationToken))
        {
            throw new CannotRemoveLastProjectManagerException(command.ProjectId);
        }

        command.Adapt(member);

        LogProjectMemberUpdated(logger, command.UserId, command.ProjectId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingProjectMember, Level = LogLevel.Information, Message = "Updating project member user ID: {UserId} on project ID: {ProjectId}")]
    private static partial void LogUpdatingProjectMember(ILogger logger, Guid userId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.ProjectMemberUpdated, Level = LogLevel.Information, Message = "Project member user ID: {UserId} updated on project ID: {ProjectId}")]
    private static partial void LogProjectMemberUpdated(ILogger logger, Guid userId, Guid projectId);
}
