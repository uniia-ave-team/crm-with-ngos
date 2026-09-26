using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.User;
using Crm.Application.Enums;
using Crm.Application.Exceptions;
using Crm.Application.Interfaces;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Extensions;
using Microsoft.AspNetCore.Identity;

namespace Crm.Infrastructure.Services;

/// <summary>
/// Provides the infrastructure-level implementation of <see cref="IIdentityService"/> using ASP.NET Core Identity.
/// Manages user roles, tokens, and other identity-related operations securely, keeping the application layer unaware of the underlying framework.
/// </summary>
/// <param name="userManager">The ASP.NET Core Identity API for managing users in the underlying store.</param>
/// <param name="userRefreshTokenRepository">The repository for managing user refresh tokens in the database.</param>
public class IdentityService(
    UserManager<AuthUser> userManager,
    IUserRefreshTokenRepository userRefreshTokenRepository) : IIdentityService
{
    /// <inheritdoc />
    public async Task<UserBasicDto> CreateUserAsync(string email, string password, User userProfile, CancellationToken cancellationToken = default)
    {
        var authUser = new AuthUser
        {
            UserName = email,
            Email = email,
            UserProfile = userProfile,
        };

        var result = await userManager.CreateAsync(authUser, password);

        return !result.Succeeded
            ? throw new UserOperationException(UserOperation.Create, result.FormatErrors())
            : new UserBasicDto(authUser.Id, authUser.Email);
    }

    /// <inheritdoc />
    public async Task<bool> CheckPasswordAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);

        return user != null && await userManager.CheckPasswordAsync(user, password);
    }

    /// <inheritdoc />
    public async Task AddToRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new EntityNotFoundException(nameof(AuthUser), userId);

        var result = await userManager.AddToRoleAsync(user, roleName);

        if (!result.Succeeded)
        {
            throw new UserOperationException(UserOperation.AssignRole, result.FormatErrors());
        }
    }

    /// <inheritdoc />
    public async Task RemoveFromRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new EntityNotFoundException(nameof(AuthUser), userId);

        var result = await userManager.RemoveFromRoleAsync(user, roleName);

        if (!result.Succeeded)
        {
            throw new UserOperationException(UserOperation.RemoveRole, result.FormatErrors());
        }
    }

    /// <inheritdoc />
    public async Task AddToRolesAsync(Guid userId, IEnumerable<string> roleNames, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new EntityNotFoundException(nameof(AuthUser), userId);

        var result = await userManager.AddToRolesAsync(user, roleNames);

        if (!result.Succeeded)
        {
            throw new UserOperationException(UserOperation.AssignRole, result.FormatErrors());
        }
    }

    /// <inheritdoc />
    public Task AddRefreshTokenAsync(Guid userId, TokenResult tokenResult, CancellationToken cancellationToken = default)
        => userRefreshTokenRepository.CreateAsync(
            userId: userId,
            refreshToken: tokenResult.Token,
            expiresAt: tokenResult.ExpiresAt,
            cancellationToken: cancellationToken);

    /// <inheritdoc />
    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new EntityNotFoundException(nameof(AuthUser), userId);

        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);

        if (!result.Succeeded)
        {
            throw new UserOperationException(UserOperation.ChangePassword, result.FormatErrors());
        }
    }

    /// <inheritdoc />
    public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new EntityNotFoundException(nameof(AuthUser), userId);

        var result = await userManager.DeleteAsync(user);

        if (!result.Succeeded)
        {
            throw new UserOperationException(UserOperation.Delete, result.FormatErrors());
        }
    }
}
