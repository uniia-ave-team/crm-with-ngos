using System.Security.Claims;
using Crm.Application.Enums;
using Crm.Application.Exceptions;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Domain.Exceptions;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Extensions;
using Microsoft.AspNetCore.Identity;

namespace Crm.Infrastructure.Services.Identity;

/// <summary>
/// Provides the infrastructure-level implementation of <see cref="IRoleIdentityService"/> using ASP.NET Core Identity.
/// </summary>
/// <param name="roleManager">The ASP.NET Core Identity API for managing roles.</param>
public sealed class RoleIdentityService(RoleManager<AuthRole> roleManager) : IRoleIdentityService
{
    /// <inheritdoc />
    public async Task<Guid> CreateRoleAsync(string roleName, string? feminitiveName = null, string? pluralName = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var role = new AuthRole
        {
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
        cancellationToken.ThrowIfCancellationRequested();

        var role = await roleManager.FindByIdAsync(roleId.ToString())
            ?? throw new EntityNotFoundException<AuthRole>(roleId);

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
        cancellationToken.ThrowIfCancellationRequested();

        var role = await roleManager.FindByIdAsync(roleId.ToString())
            ?? throw new EntityNotFoundException<AuthRole>(roleId);

        var result = await roleManager.DeleteAsync(role);

        if (!result.Succeeded)
        {
            throw new UserOperationException(UserOperation.RemoveRole, result.FormatErrors());
        }
    }

    /// <inheritdoc />
    public async Task AddPermissionToRoleAsync(Guid roleId, string permissionValue, CancellationToken cancellationToken = default)
        => await AddPermissionsToRoleAsync(roleId, [permissionValue], cancellationToken);

    /// <inheritdoc />
    public async Task<int> AddPermissionsToRoleAsync(Guid roleId, IEnumerable<string> permissionValues, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var role = await roleManager.FindByIdAsync(roleId.ToString())
            ?? throw new EntityNotFoundException<AuthRole>(roleId);

        var existingClaims = await roleManager.GetClaimsAsync(role);
        var existingPermissionSet = existingClaims
            .Where(c => c.Type == CustomClaimTypes.Permission)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);

        int addedCount = 0;

        foreach (string permissionValue in permissionValues.Distinct(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!existingPermissionSet.Add(permissionValue))
            {
                continue;
            }

            var result = await roleManager.AddClaimAsync(role, new Claim(CustomClaimTypes.Permission, permissionValue));
            if (!result.Succeeded)
            {
                throw new UserOperationException(UserOperation.Update, result.FormatErrors());
            }

            addedCount++;
        }

        return addedCount;
    }

    /// <inheritdoc />
    public async Task RemovePermissionFromRoleAsync(Guid roleId, string permissionValue, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var role = await roleManager.FindByIdAsync(roleId.ToString())
            ?? throw new EntityNotFoundException<AuthRole>(roleId);

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
        cancellationToken.ThrowIfCancellationRequested();

        var role = await roleManager.FindByIdAsync(roleId.ToString())
            ?? throw new EntityNotFoundException<AuthRole>(roleId);

        var claims = await roleManager.GetClaimsAsync(role);

        return claims
            .Where(c => c.Type == CustomClaimTypes.Permission)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);
    }
}
