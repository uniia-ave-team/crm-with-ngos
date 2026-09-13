using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Role.Commands;
using Crm.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Roles.Commands;

/// <summary>
/// Handles the <see cref="CreateRoleCommand"/> to create a new Identity Role in the system.
/// </summary>
/// <param name="roleManager">The ASP.NET Core Identity role manager used for role creation and validation.</param>
/// <param name="logger">The logger used to record the lifecycle and outcome of the role creation process.</param>
public partial class CreateRoleCommandHandler(
    RoleManager<AuthRole> roleManager,
    ILogger<CreateRoleCommandHandler> logger) : IRequestHandler<CreateRoleCommand, Guid>
{
    public async Task<Guid> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        LogCreatingRole(logger, request.Name);

        var role = new AuthRole()
        {
            Name = request.Name,
        };

        var result = await roleManager.CreateAsync(role);

        if (!result.Succeeded)
        {
            string errors = string.Join(" | ", result.Errors.Select(e => e.Description));
            LogRoleCreationFailed(logger, request.Name, errors);

            throw new InvalidOperationException($"Role creation failed: {errors}");
        }

        LogRoleCreatedSuccessfully(logger, role.Name, role.Id);

        return role.Id;
    }

    [LoggerMessage(EventId = LogEventIds.CreatingRole, Level = LogLevel.Information, Message = "Initiating creation of role: {RoleName}")]
    private static partial void LogCreatingRole(ILogger logger, string roleName);

    [LoggerMessage(EventId = LogEventIds.RoleCreationFailed, Level = LogLevel.Warning, Message = "Failed to create role '{RoleName}'. Reason: {Errors}")]
    private static partial void LogRoleCreationFailed(ILogger logger, string roleName, string errors);

    [LoggerMessage(EventId = LogEventIds.RoleCreatedSuccessfully, Level = LogLevel.Information, Message = "Role '{RoleName}' successfully created with ID: {RoleId}")]
    private static partial void LogRoleCreatedSuccessfully(ILogger logger, string roleName, Guid roleId);
}
