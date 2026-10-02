using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="CreateUserCommand"/> to create a new Identity user along with their domain profile.
/// Restricted to only work for the first user initialization.
/// </summary>
public partial class CreateUserCommandHandler(
    IIdentityService identityService,
    IAuthRoleRepository authRoleRepository,
    IUserRepository userRepository,
    TimeProvider timeProvider,
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

            var user = await CreateUserAsync(request, cancellationToken);
            await AssignAdminRoleAsync(user.Id, cancellationToken);

            LogUserCreatedSuccessfully(logger, request.Email, user.Id);

            return user.Id;
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

    private async Task<UserBasicDto> CreateUserAsync(CreateUserCommand request, CancellationToken cancellationToken = default)
    {
        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            IsActive = true,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        return await identityService.CreateUserAsync(request.Email, request.Password, user, cancellationToken);
    }

    private async Task AssignAdminRoleAsync(Guid userId, CancellationToken cancellationToken)
    {
        await EnsureAdminRoleExistsAsync(cancellationToken);

        await identityService.AddToRoleAsync(userId, RoleConsts.Admin, cancellationToken);
    }

    private async Task EnsureAdminRoleExistsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await authRoleRepository.EnsureExistsAsync(RoleConsts.Admin, cancellationToken);
        }
        catch (EntityNotFoundException)
        {
            LogAdminRoleNotFound(logger, RoleConsts.Admin);
            throw new InvalidOperationException($"Critical initialization error: Role '{RoleConsts.Admin}' does not exist.");
        }
    }

    [LoggerMessage(EventId = LogEventIds.CreatingUser, Level = LogLevel.Information, Message = "Initiating creation of user with email: {Email}")]
    private static partial void LogCreatingUser(ILogger logger, string email);

    [LoggerMessage(EventId = LogEventIds.UserCreatedSuccessfully, Level = LogLevel.Information, Message = "User '{Email}' successfully created with ID: {UserId}")]
    private static partial void LogUserCreatedSuccessfully(ILogger logger, string email, Guid userId);

    [LoggerMessage(EventId = LogEventIds.AdminRoleNotFound, Level = LogLevel.Error, Message = "Cannot assign role '{RoleName}' because it does not exist in the database. Ensure seeders are run before user creation.")]
    private static partial void LogAdminRoleNotFound(ILogger logger, string roleName);
}
