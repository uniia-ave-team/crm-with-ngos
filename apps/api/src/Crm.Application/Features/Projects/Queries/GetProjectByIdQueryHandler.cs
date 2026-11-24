using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Projects;
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
/// <param name="fileUrlProvider">The file URL provider for resolving member avatar URLs.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class GetProjectByIdQueryHandler(
    IProjectRepository projectRepository,
    IFileUrlProvider fileUrlProvider,
    ILogger<GetProjectByIdQueryHandler> logger) : IQueryHandler<GetProjectByIdQuery, ProjectDto>
{
    /// <inheritdoc />
    public async ValueTask<ProjectDto> Handle(GetProjectByIdQuery query, CancellationToken cancellationToken)
    {
        LogFetchingProjectById(logger, query.Id, query.CurrentUserId);

        var project = await projectRepository.GetAsync<ProjectDto>(query.Id, cancellationToken);
        var result = project.EnrichUrls(fileUrlProvider, query.CurrentUserId);

        LogProjectByIdFetched(logger, query.Id);

        return result;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingProjectById, Level = LogLevel.Information, Message = "Fetching project ID: {ProjectId} for user ID: {UserId}")]
    private static partial void LogFetchingProjectById(ILogger logger, Guid projectId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.ProjectByIdFetched, Level = LogLevel.Information, Message = "Project ID: {ProjectId} successfully fetched.")]
    private static partial void LogProjectByIdFetched(ILogger logger, Guid projectId);
}
