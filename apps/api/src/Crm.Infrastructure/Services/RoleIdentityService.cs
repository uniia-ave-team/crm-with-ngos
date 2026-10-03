using System.Security.Claims;
using Crm.Application.Enums;
using Crm.Application.Exceptions;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Exceptions;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Extensions;
using Microsoft.AspNetCore.Identity;

namespace Crm.Infrastructure.Services;

/// <summary>
/// Provides the infrastructure-level implementation of <see cref="IRoleIdentityService"/> using ASP.NET Core Identity.
/// </summary>
/// <param name="roleManager">The ASP.NET Core Identity API for managing roles.</param>
public class RoleIdentityService(RoleManager<AuthRole> roleManager) : IRoleIdentityService
{
    /// <inheritdoc />
    public async Task<Guid> CreateRoleAsync(string roleName, string? feminitiveName = null, string? pluralName = null, CancellationToken cancellationToken = default)
    {
        var role = new AuthRole
        {
            Id = Guid.NewGuid(),
            Name = roleName,
            FeminitiveName = feminitiveName,
            PluralName = pluralName,
        };

        var result = await roleManager.CreateAsync(role);

        return !result.Succeeded ? throw new UserOperationException(UserOperation.Create, result.FormatErrors()) : role.Id;
    }

    /// <inheritdoc />
    public async Task UpdateRoleAsync(Guid roleId, string roleName, string? feminitiveName = null, string? pluralName = null, CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString())
            ?? throw new EntityNotFoundException(nameof(AuthRole), roleId);

        role.Name = roleName;
        role.FeminitiveName = feminitiveName;
        role.PluralName = pluralName;

        var result = await roleManager.UpdateAsync(role);

        if (!result.Succeeded)
        {
            throw new UserOperationException(UserOperation.Update, result.FormatErrors());
        }
    }

    /// <inheritdoc />
    public async Task DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString())
            ?? throw new EntityNotFoundException(nameof(AuthRole), roleId);

        var result = await roleManager.DeleteAsync(role);

        if (!result.Succeeded)
        {
            throw new UserOperationException(UserOperation.RemoveRole, result.FormatErrors());
        }
    }

    /// <inheritdoc />
    public async Task AddPermissionToRoleAsync(Guid roleId, string permissionValue, CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString())
            ?? throw new EntityNotFoundException(nameof(AuthRole), roleId);

        var claim = new Claim(CustomClaimTypes.Permission, permissionValue);

        var result = await roleManager.AddClaimAsync(role, claim);

        if (!result.Succeeded)
        {
            throw new UserOperationException(UserOperation.Update, result.FormatErrors());
        }
    }

    /// <inheritdoc />
    public async Task RemovePermissionFromRoleAsync(Guid roleId, string permissionValue, CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString())
            ?? throw new EntityNotFoundException(nameof(AuthRole), roleId);

        var claim = new Claim(CustomClaimTypes.Permission, permissionValue);

        var result = await roleManager.RemoveClaimAsync(role, claim);

        if (!result.Succeeded)
        {
            throw new UserOperationException(UserOperation.Update, result.FormatErrors());
        }
    }

    /// <inheritdoc />
    public async Task<HashSet<string>> GetRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString())
            ?? throw new EntityNotFoundException(nameof(AuthRole), roleId);

        var claims = await roleManager.GetClaimsAsync(role);

        return [.. claims
            .Where(c => c.Type == CustomClaimTypes.Permission)
            .Select(c => c.Value)];
    }
}
