using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Role;
using Crm.Application.Dtos.Role.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Roles.Queries;

/// <summary>
/// Handles the <see cref="GetRoleByIdQuery"/> to retrieve a role and its associated claims.
/// </summary>
public partial class GetRoleByIdQueryHandler(
    IAuthRoleRepository authRoleRepository,
    IRoleIdentityService identityService,
    ILogger<GetRoleByIdQueryHandler> logger) : IRequestHandler<GetRoleByIdQuery, RoleDetailsDto>
{
    public async Task<RoleDetailsDto> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        LogFetchingRoleDetails(logger, request.Id);

        var role = await authRoleRepository.GetAsync<RoleDto>(request.Id, cancellationToken);

        var roleClaims = await identityService.GetRolePermissionsAsync(role.Id, cancellationToken);

        LogRoleDetailsFetched(logger, request.Id, roleClaims.Count);

        return role.Adapt<RoleDetailsDto>() with { Claims = roleClaims };
    }

    [LoggerMessage(EventId = LogEventIds.FetchingRoleDetails, Level = LogLevel.Information, Message = "Fetching details for role ID {RoleId}.")]
    private static partial void LogFetchingRoleDetails(ILogger logger, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.RoleDetailsFetched, Level = LogLevel.Information, Message = "Successfully retrieved role details for ID {RoleId} with {ClaimCount} claim(s).")]
    private static partial void LogRoleDetailsFetched(ILogger logger, Guid roleId, int claimCount);
}
