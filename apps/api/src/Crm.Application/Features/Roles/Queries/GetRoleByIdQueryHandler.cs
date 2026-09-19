using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Role;
using Crm.Application.Dtos.Role.Queries;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Roles.Queries;

/// <summary>
/// Handles the <see cref="GetRoleByIdQuery"/> to retrieve a role and its associated claims.
/// </summary>
public partial class GetRoleByIdQueryHandler(
    RoleManager<AuthRole> roleManager,
    ILogger<GetRoleByIdQueryHandler> logger) : IRequestHandler<GetRoleByIdQuery, RoleDetailsDto>
{
    public async Task<RoleDetailsDto> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        LogFetchingRoleDetails(logger, request.Id);

        var role = await roleManager.FindByIdAsync(request.Id.ToString());

        if (role == null)
        {
            LogRoleNotFound(logger, request.Id);
            throw new EntityNotFoundException(nameof(AuthRole), request.Id);
        }

        var roleClaims = await roleManager.GetClaimsAsync(role);

        var claimNames = roleClaims.Select(c => c.Value).ToList();

        LogRoleDetailsFetched(logger, request.Id, claimNames.Count);

        return role.Adapt<RoleDetailsDto>() with { Claims = claimNames };
    }

    [LoggerMessage(EventId = LogEventIds.FetchingRoleDetails, Level = LogLevel.Information, Message = "Fetching details for role ID {RoleId}.")]
    private static partial void LogFetchingRoleDetails(ILogger logger, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.GetRoleByIdQueryHandlerRoleNotFound, Level = LogLevel.Warning, Message = "Role with ID {RoleId} was not found.")]
    private static partial void LogRoleNotFound(ILogger logger, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.RoleDetailsFetched, Level = LogLevel.Information, Message = "Successfully retrieved role details for ID {RoleId} with {ClaimCount} claim(s).")]
    private static partial void LogRoleDetailsFetched(ILogger logger, Guid roleId, int claimCount);
}
