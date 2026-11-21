using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Project;
using Crm.Application.Dtos.Project.Queries;
using Crm.Application.Features.Projects.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Projects.Queries;

/// <summary>
/// Handles the <see cref="GetProjectByIdQuery"/> to retrieve detailed project information.
/// </summary>
/// <param name="projectRepository">The repository for projecting project details.</param>
/// <param name="currentUserService">The service for retrieving the currently authenticated user's identifier.</param>
/// <param name="fileUrlProvider">The file URL provider for resolving member avatar URLs.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class GetProjectByIdQueryHandler(
    IProjectRepository projectRepository,
    ICurrentUserService currentUserService,
    IFileUrlProvider fileUrlProvider,
    ILogger<GetProjectByIdQueryHandler> logger) : IRequestHandler<GetProjectByIdQuery, ProjectDto>
{
    /// <inheritdoc />
    public async ValueTask<ProjectDto> Handle(GetProjectByIdQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.GetUserId();
        LogFetchingProjectById(logger, request.Id, currentUserId);

        var project = await projectRepository.GetAsync<ProjectDto>(request.Id, cancellationToken);
        var result = project.EnrichUrls(fileUrlProvider, currentUserId);

        LogProjectByIdFetched(logger, request.Id);

        return result;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingProjectById, Level = LogLevel.Information, Message = "Fetching project ID: {ProjectId} for user ID: {UserId}")]
    private static partial void LogFetchingProjectById(ILogger logger, Guid projectId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.ProjectByIdFetched, Level = LogLevel.Information, Message = "Project ID: {ProjectId} successfully fetched.")]
    private static partial void LogProjectByIdFetched(ILogger logger, Guid projectId);
}
