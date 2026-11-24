using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Projects;
using Crm.Application.Features.Projects.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Projects.Queries;

/// <summary>
/// Handles the <see cref="GetProjectMembersQuery"/> to retrieve all members of a project.
/// </summary>
/// <param name="projectRepository">The repository for projecting project members.</param>
/// <param name="fileUrlProvider">The file URL provider for resolving member avatar URLs.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class GetProjectMembersQueryHandler(
    IProjectRepository projectRepository,
    IFileUrlProvider fileUrlProvider,
    ILogger<GetProjectMembersQueryHandler> logger) : IQueryHandler<GetProjectMembersQuery, List<ProjectMemberDto>>
{
    /// <inheritdoc />
    public async ValueTask<List<ProjectMemberDto>> Handle(GetProjectMembersQuery query, CancellationToken cancellationToken)
    {
        LogFetchingProjectMembers(logger, query.ProjectId);

        var members = await projectRepository.GetMembersAsync<ProjectMemberDto>(query.ProjectId, cancellationToken);
        var result = members.EnrichUrls(fileUrlProvider, query.CurrentUserId);

        LogProjectMembersFetched(logger, result.Count, query.ProjectId);

        return result;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingProjectMembers, Level = LogLevel.Information, Message = "Fetching members for project ID: {ProjectId}")]
    private static partial void LogFetchingProjectMembers(ILogger logger, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.ProjectMembersFetched, Level = LogLevel.Information, Message = "Retrieved {Count} members for project ID: {ProjectId}")]
    private static partial void LogProjectMembersFetched(ILogger logger, int count, Guid projectId);
}
