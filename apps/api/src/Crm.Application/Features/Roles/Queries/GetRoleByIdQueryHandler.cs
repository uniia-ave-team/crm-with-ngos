using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Roles;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Roles.Queries;

/// <summary>
/// Handles the <see cref="GetRoleByIdQuery"/> to retrieve a role and its associated claims.
/// </summary>
/// <param name="authRoleRepository">The repository used to retrieve role details.</param>
/// <param name="identityService">The service used to retrieve permission claims assigned to the role.</param>
/// <param name="logger">The logger used to record the execution of the query.</param>
public sealed partial class GetRoleByIdQueryHandler(
    IAuthRoleRepository authRoleRepository,
    IRoleIdentityService identityService,
    ILogger<GetRoleByIdQueryHandler> logger) : IQueryHandler<GetRoleByIdQuery, RoleDetailsDto>
{
    public async ValueTask<RoleDetailsDto> Handle(GetRoleByIdQuery query, CancellationToken cancellationToken)
    {
        LogFetchingRoleDetails(logger, query.Id);

        var role = await authRoleRepository.GetAsync<RoleDto>(query.Id, cancellationToken);

        var roleClaims = await identityService.GetRolePermissionsAsync(role.Id, cancellationToken);

        LogRoleDetailsFetched(logger, query.Id, roleClaims.Count);

        return new RoleDetailsDto(role.Id, role.Name, role.FeminitiveName, role.PluralName, roleClaims);
    }

    [LoggerMessage(EventId = LogEventIds.FetchingRoleDetails, Level = LogLevel.Information, Message = "Fetching details for role ID {RoleId}.")]
    private static partial void LogFetchingRoleDetails(ILogger logger, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.RoleDetailsFetched, Level = LogLevel.Information, Message = "Successfully retrieved role details for ID {RoleId} with {ClaimCount} claim(s).")]
    private static partial void LogRoleDetailsFetched(ILogger logger, Guid roleId, int claimCount);
}
