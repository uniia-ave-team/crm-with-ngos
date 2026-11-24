using Crm.Application.Common.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Projects.Commands;

/// <summary>
/// Handles the <see cref="CreateProjectCommand"/> to create a new project and register its creator.
/// </summary>
/// <param name="projectRepository">The repository for persisting project entities.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class CreateProjectCommandHandler(
    IProjectRepository projectRepository,
    ILogger<CreateProjectCommandHandler> logger) : ICommandHandler<CreateProjectCommand, Guid>
{
    /// <inheritdoc />
    public async ValueTask<Guid> Handle(CreateProjectCommand command, CancellationToken cancellationToken)
    {
        LogCreatingProject(logger, command.CurrentUserId);

        var project = command.Adapt<Project>();
        project.EnsureValidDateRange();
        project.CreatedBy = command.CurrentUserId;

        project.Members.Add(new()
        {
            RoleTitle = string.Empty,
            UserId = command.CurrentUserId,
            Claim = ProjectClaim.Manage,
        });

        await projectRepository.CreateAsync(project, cancellationToken);

        LogProjectCreatedSuccessfully(logger, project.Id);

        return project.Id;
    }

    [LoggerMessage(EventId = LogEventIds.CreatingProject, Level = LogLevel.Information, Message = "Creating project by user ID: {UserId}")]
    private static partial void LogCreatingProject(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.ProjectCreatedSuccessfully, Level = LogLevel.Information, Message = "Project successfully created with ID: {ProjectId}")]
    private static partial void LogProjectCreatedSuccessfully(ILogger logger, Guid projectId);
}
