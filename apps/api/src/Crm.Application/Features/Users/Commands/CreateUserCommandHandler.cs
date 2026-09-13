using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User.Commands;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="CreateUserCommand"/> to create a new Identity user along with their domain profile.
/// Restricted to only work for the first user initialization.
/// </summary>
public partial class CreateUserCommandHandler(
    UserManager<AuthUser> userManager,
    RoleManager<AuthRole> roleManager,
    IUserRepository userRepository,
    ILogger<CreateUserCommandHandler> logger) : IRequestHandler<CreateUserCommand, Guid>
{
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<Guid> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        LogCreatingUser(logger, request.Email);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await GuardAgainstExistingUsersAsync(cancellationToken);

            var authUser = BuildAuthUser(request);

            await PersistIdentityUserAsync(authUser, request.Password);
            await AssignAdminRoleAsync(authUser, request.Email);

            LogUserCreatedSuccessfully(logger, request.Email, authUser.Id);

            return authUser.Id;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task GuardAgainstExistingUsersAsync(CancellationToken cancellationToken)
    {
        if (await userRepository.AnyAsync(cancellationToken))
        {
            throw new EntityAlreadyExistsException(nameof(User));
        }
    }

    private static AuthUser BuildAuthUser(CreateUserCommand request)
    {
        return new AuthUser
        {
            UserName = request.Email,
            Email = request.Email,
            UserProfile = new User
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                IsActive = true,
            },
        };
    }

    private async Task PersistIdentityUserAsync(AuthUser user, string password)
    {
        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            string errors = string.Join(" | ", result.Errors.Select(e => e.Description));
            LogUserCreationFailed(logger, user.Email!, errors);

            throw new InvalidOperationException($"User creation failed: {errors}");
        }
    }

    private async Task AssignAdminRoleAsync(AuthUser user, string email)
    {
        if (!await roleManager.RoleExistsAsync(RoleConsts.Admin))
        {
            LogAdminRoleNotFound(logger, RoleConsts.Admin);
            throw new InvalidOperationException($"Critical initialization error: Role '{RoleConsts.Admin}' does not exist.");
        }

        var result = await userManager.AddToRoleAsync(user, RoleConsts.Admin);

        if (!result.Succeeded)
        {
            string errors = string.Join(" | ", result.Errors.Select(e => e.Description));
            LogRoleAssignmentFailed(logger, email, RoleConsts.Admin, errors);

            throw new InvalidOperationException($"Failed to assign Admin role: {errors}");
        }
    }

    [LoggerMessage(EventId = LogEventIds.CreatingUser, Level = LogLevel.Information, Message = "Initiating creation of user with email: {Email}")]
    private static partial void LogCreatingUser(ILogger logger, string email);

    [LoggerMessage(EventId = LogEventIds.UserCreationFailed, Level = LogLevel.Warning, Message = "Failed to create user '{Email}'. Reason: {Errors}")]
    private static partial void LogUserCreationFailed(ILogger logger, string email, string errors);

    [LoggerMessage(EventId = LogEventIds.UserCreatedSuccessfully, Level = LogLevel.Information, Message = "User '{Email}' successfully created with ID: {UserId}")]
    private static partial void LogUserCreatedSuccessfully(ILogger logger, string email, Guid userId);

    [LoggerMessage(EventId = LogEventIds.CreateUserCommandHandlerRoleAssignmentFailed, Level = LogLevel.Warning, Message = "Failed to assign role '{RoleName}' to user '{Email}'. Reason: {Errors}")]
    private static partial void LogRoleAssignmentFailed(ILogger logger, string email, string roleName, string errors);

    [LoggerMessage(EventId = LogEventIds.AdminRoleNotFound, Level = LogLevel.Error, Message = "Cannot assign role '{RoleName}' because it does not exist in the database. Ensure seeders are run before user creation.")]
    private static partial void LogAdminRoleNotFound(ILogger logger, string roleName);
}
