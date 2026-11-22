using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Users;
using Crm.Application.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="CreateUserCommand"/> to create a new Identity user along with their domain profile.
/// Restricted to only work for the first user initialization.
/// </summary>
public sealed partial class CreateUserCommandHandler(
    IIdentityService identityService,
    IAuthRoleRepository authRoleRepository,
    IUserRepository userRepository,
    TimeProvider timeProvider,
    ILogger<CreateUserCommandHandler> logger) : ICommandHandler<CreateUserCommand, Guid>
{
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public async ValueTask<Guid> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var maskedEmail = command.Email.MaskEmail();

        LogCreatingUser(logger, maskedEmail);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await GuardAgainstExistingUsersAsync(cancellationToken);

            var user = await CreateUserAsync(command, cancellationToken);
            await AssignAdminRoleAsync(user.Id, cancellationToken);

            LogUserCreatedSuccessfully(logger, maskedEmail, user.Id);

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

    private async Task<UserBasicDto> CreateUserAsync(CreateUserCommand command, CancellationToken cancellationToken = default)
    {
        var user = command.Adapt<User>();

        user.IsActive = true;
        user.CreatedAt = timeProvider.GetUtcNow().UtcDateTime;

        return await identityService.CreateUserAsync(command.Email, command.Password, user, cancellationToken);
    }

    private async Task AssignAdminRoleAsync(Guid userId, CancellationToken cancellationToken)
    {
        await EnsureAdminRoleExistsAsync(cancellationToken);

        await identityService.AddToRoleAsync(userId, RoleConstants.Admin, cancellationToken);
    }

    private async Task EnsureAdminRoleExistsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await authRoleRepository.EnsureExistsAsync(RoleConstants.Admin, cancellationToken);
        }
        catch (NotFoundException)
        {
            LogAdminRoleNotFound(logger, RoleConstants.Admin);
            throw new InvalidOperationException($"Critical initialization error: Role '{RoleConstants.Admin}' does not exist.");
        }
    }

    [LoggerMessage(EventId = LogEventIds.CreatingUser, Level = LogLevel.Information, Message = "Initiating creation of user with email: {Email}")]
    private static partial void LogCreatingUser(ILogger logger, string email);

    [LoggerMessage(EventId = LogEventIds.UserCreatedSuccessfully, Level = LogLevel.Information, Message = "User '{Email}' successfully created with ID: {UserId}")]
    private static partial void LogUserCreatedSuccessfully(ILogger logger, string email, Guid userId);

    [LoggerMessage(EventId = LogEventIds.AdminRoleNotFound, Level = LogLevel.Error, Message = "Cannot assign role '{RoleName}' because it does not exist in the database. Ensure seeders are run before user creation.")]
    private static partial void LogAdminRoleNotFound(ILogger logger, string roleName);
}
