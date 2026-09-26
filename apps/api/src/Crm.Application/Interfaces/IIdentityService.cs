using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.User;
using Crm.Application.Exceptions;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;

namespace Crm.Application.Interfaces;

/// <summary>
/// Defines a service for managing user identities, roles, and security credentials.
/// Abstracts the underlying identity provider infrastructure away from the application layer.
/// </summary>
public interface IIdentityService
{
    /// <summary>
    /// Asynchronously creates a new authenticated user with the specified email, password, and associated user profile.
    /// </summary>
    /// <param name="email">The email address, which also serves as the username for the new user.</param>
    /// <param name="password">The password to set for the new user.</param>
    /// <param name="userProfile">The associated user profile containing personal information and settings.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="UserBasicDto"/> representing the newly created user.</returns>
    /// <exception cref="UserOperationException">Thrown if the user creation process fails (e.g., due to password complexity requirements or duplicate email).</exception>
    Task<UserBasicDto> CreateUserAsync(string email, string password, User userProfile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously verifies whether the provided password is valid for the user with the specified email address.
    /// </summary>
    /// <param name="email">The email address of the user to authenticate.</param>
    /// <param name="password">The plain-text password to verify.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains <c>true</c> if the user exists and the password is correct; otherwise, <c>false</c>.
    /// </returns>
    Task<bool> CheckPasswordAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously assigns a specified role to a user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to whom the role will be assigned.</param>
    /// <param name="roleName">The name of the role to assign.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the user with the specified ID does not exist.</exception>
    /// <exception cref="UserOperationException">Thrown if the role assignment operation fails (e.g., role does not exist, user already has the role).</exception>
    Task AddToRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes a specified role from a user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user from whom the role will be removed.</param>
    /// <param name="roleName">The name of the role to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the user with the specified ID does not exist.</exception>
    /// <exception cref="UserOperationException">Thrown if the role removal operation fails (e.g., role does not exist, user does not have the role).</exception>
    Task RemoveFromRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously assigns a specified collection of roles to a user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to whom the roles will be assigned.</param>
    /// <param name="roleNames">The collection of role names to assign.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the user with the specified ID does not exist.</exception>
    /// <exception cref="UserOperationException">Thrown if the role assignment operation fails (e.g., roles do not exist, user already has the roles).</exception>
    Task AddToRolesAsync(Guid userId, IEnumerable<string> roleNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously adds the refresh token and its expiration time for a specific user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="tokenResult">The generated token result containing the new refresh token value and its expiration details.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task AddRefreshTokenAsync(Guid userId, TokenResult tokenResult, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously changes the password for a specified user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="currentPassword">The user's current password.</param>
    /// <param name="newPassword">The new password to set.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the user with the specified ID does not exist.</exception>
    /// <exception cref="UserOperationException">Thrown if the password change operation fails (e.g., incorrect current password, or the new password does not meet complexity requirements).</exception>
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
}
