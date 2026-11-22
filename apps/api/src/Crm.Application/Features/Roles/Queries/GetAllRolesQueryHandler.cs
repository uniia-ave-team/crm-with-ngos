using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Roles;
using Crm.Domain.Common;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Roles.Queries;

/// <summary>
/// Handles the <see cref="GetAllRolesQuery"/> to retrieve a paginated list of identity roles.
/// </summary>
/// <param name="authRoleRepository">The repository used to retrieve paginated role data.</param>
/// <param name="logger">The logger used to record the execution of the role retrieval query.</param>
public sealed partial class GetAllRolesQueryHandler(
    IAuthRoleRepository authRoleRepository,
    ILogger<GetAllRolesQueryHandler> logger) : IQueryHandler<GetAllRolesQuery, PagedResult<RoleDto>>
{
    public async ValueTask<PagedResult<RoleDto>> Handle(GetAllRolesQuery query, CancellationToken cancellationToken)
    {
        bool hasSearchFilter = !string.IsNullOrWhiteSpace(query.SearchTerm);
        LogFetchingRoles(logger, query.PageNumber, query.PageSize, hasSearchFilter);

        var pagedResult = await authRoleRepository.GetPagedAsync<RoleDto>(
            searchTerm: query.SearchTerm,
            orderBy: query.OrderBy,
            sortOrder: query.SortOrder,
            pageNumber: query.PageNumber,
            pageSize: query.PageSize,
            cancellationToken: cancellationToken);

        LogRolesFetched(logger, pagedResult.Items.Count, pagedResult.TotalCount);

        return pagedResult;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingRoles, Level = LogLevel.Information, Message = "Fetching roles - Page: {PageNumber}, Size: {PageSize}, HasSearchFilter: {HasSearchFilter}")]
    private static partial void LogFetchingRoles(ILogger logger, int pageNumber, int pageSize, bool hasSearchFilter);

    [LoggerMessage(EventId = LogEventIds.RolesFetched, Level = LogLevel.Information, Message = "Successfully retrieved {FetchedCount} roles out of {TotalCount} total matched.")]
    private static partial void LogRolesFetched(ILogger logger, int fetchedCount, int totalCount);
}
