using Crm.Application.Common.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Projects.Commands;

/// <summary>
/// Handles the <see cref="AssignProjectMemberCommand"/> to assign an active user to a project
/// with a functional role title and project permission claim.
/// </summary>
/// <param name="projectRepository">The repository for managing project and project member data.</param>
/// <param name="userRepository">The repository for verifying user existence and active status.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class AssignProjectMemberCommandHandler(
    IProjectRepository projectRepository,
    IUserRepository userRepository,
    ILogger<AssignProjectMemberCommandHandler> logger) : ICommandHandler<AssignProjectMemberCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(AssignProjectMemberCommand command, CancellationToken cancellationToken)
    {
        LogAssigningProjectMember(logger, command.UserId, command.ProjectId);

        if (!await userRepository.IsActiveAsync(command.UserId, cancellationToken))
        {
            throw new CannotAssignUserToProjectException(command.UserId, AssignmentErrorMessages.UserAccountDeactivated);
        }

        if (await projectRepository.MemberExistsAsync(command.ProjectId, command.UserId, cancellationToken))
        {
            throw new EntityAlreadyExistsException(nameof(ProjectMember), $"{command.ProjectId}:{command.UserId}");
        }

        var member = command.Adapt<ProjectMember>();
        await projectRepository.AddMemberAsync(member, cancellationToken);

        LogProjectMemberAssigned(logger, command.UserId, command.ProjectId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.AssigningProjectMember, Level = LogLevel.Information, Message = "Assigning user ID: {UserId} to project ID: {ProjectId}")]
    private static partial void LogAssigningProjectMember(ILogger logger, Guid userId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.ProjectMemberAssigned, Level = LogLevel.Information, Message = "User ID: {UserId} successfully assigned to project ID: {ProjectId}")]
    private static partial void LogProjectMemberAssigned(ILogger logger, Guid userId, Guid projectId);
}
