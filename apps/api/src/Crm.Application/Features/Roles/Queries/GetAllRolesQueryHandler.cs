using System.Linq.Expressions;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Role;
using Crm.Application.Dtos.Role.Queries;
using Crm.Application.Features.Roles.Mappings;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Roles.Queries;

/// <summary>
/// Handles the <see cref="GetAllRolesQuery"/> to retrieve a paginated list of identity roles.
/// </summary>
public partial class GetAllRolesQueryHandler(
    IAuthRoleRepository authRoleRepository,
    ILogger<GetAllRolesQueryHandler> logger) : IRequestHandler<GetAllRolesQuery, PagedResult<RoleDto>>
{
    public async Task<PagedResult<RoleDto>> Handle(GetAllRolesQuery request, CancellationToken cancellationToken)
    {
        LogFetchingRoles(logger, request.PageNumber, request.PageSize, request.SearchTerm);

        var filter = BuildRoleFilter(request.SearchTerm);

        var pagedResult = await authRoleRepository.GetPagedAsync(
            selector: RoleMappingExtensions.ToDtoExpression(),
            predicate: filter,
            orderBy: request.OrderBy,
            sortOrder: request.SortOrder,
            pageNumber: request.PageNumber,
            pageSize: request.PageSize,
            ct: cancellationToken);

        LogRolesFetched(logger, pagedResult.Items.Count, pagedResult.TotalCount);

        return pagedResult;
    }

    /// <summary>
    /// Builds the filter expression for querying roles based on an optional search term.
    /// </summary>
    private static Expression<Func<AuthRole, bool>>? BuildRoleFilter(string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return null;
        }

        string term = searchTerm.Trim().ToUpperInvariant();

        return role => role.NormalizedName != null && role.NormalizedName.Contains(term);
    }

    [LoggerMessage(EventId = LogEventIds.FetchingRoles, Level = LogLevel.Information, Message = "Fetching roles - Page: {PageNumber}, Size: {PageSize}, Search: {SearchTerm}")]
    private static partial void LogFetchingRoles(ILogger logger, int pageNumber, int pageSize, string? searchTerm);

    [LoggerMessage(EventId = LogEventIds.RolesFetched, Level = LogLevel.Information, Message = "Successfully retrieved {FetchedCount} roles out of {TotalCount} total matched.")]
    private static partial void LogRolesFetched(ILogger logger, int fetchedCount, int totalCount);
}
