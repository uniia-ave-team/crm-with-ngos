using System.Security.Authentication;
using System.Security.Claims;
using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Commands;
using Crm.Application.Enums;
using Crm.Application.Exceptions;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Options;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.Options;

namespace Crm.Api.DemoMode;

/// <summary>
/// Intercepts sensitive identity and token-issuance commands during Demo Mode dry-run execution so they:
/// <list type="bullet">
/// <item><description>Run full FluentValidation and read-only domain checks (preserving 400/401/404/409 behavior).</description></item>
/// <item><description>Avoid executing <c>UPDATE</c>/<c>DELETE</c> DML on <c>AuthUsers</c> and <c>UserRefreshTokens</c> inside PostgreSQL (preventing row locks and deadlocks on the shared demo account).</description></item>
/// <item><description>Never mint real cryptographically signed invitation/password-reset/phantom-admin JWT tokens in Demo Mode.</description></item>
/// </list>
/// </summary>
/// <param name="serviceProvider">The scoped service provider used to resolve validators and read-only repositories.</param>
/// <param name="timeProvider">The time provider used for synthetic demo token expiration timestamps.</param>
/// <param name="logger">The logger recording intercepted demo commands.</param>
/// <param name="jwtOptions">Optional JWT options used for token expiration durations.</param>
public sealed partial class DemoSafeIdentityCommandInterceptor(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    ILogger<DemoSafeIdentityCommandInterceptor> logger,
    IOptions<JwtOptions>? jwtOptions = null) : IDemoCommandInterceptor
{
    private const int DefaultInvitationExpiryHours = 48;
    private const int DefaultResetPasswordExpiryHours = 72;

    /// <inheritdoc />
    public async ValueTask<(bool Handled, TResponse Response)> TryHandleAsync<TResponse>(
        object message,
        DemoExecutionMode mode,
        CancellationToken cancellationToken)
    {
        if (mode != DemoExecutionMode.DryRun)
        {
            return (false, default!);
        }

        switch (message)
        {
            case UpdatePasswordCommand updatePasswordCmd when typeof(TResponse) == typeof(Unit):
                await HandleUpdatePasswordAsync(updatePasswordCmd, cancellationToken);
                return (true, (TResponse)(object)Unit.Value);

            case DeleteUserCommand deleteUserCmd when typeof(TResponse) == typeof(Unit):
                await HandleDeleteUserAsync(deleteUserCmd, cancellationToken);
                return (true, (TResponse)(object)Unit.Value);

            case ResetPasswordCommand resetPasswordCmd when typeof(TResponse) == typeof(Unit):
                await HandleResetPasswordAsync(resetPasswordCmd, cancellationToken);
                return (true, (TResponse)(object)Unit.Value);

            case GenerateRestorePasswordTokenCommand restoreTokenCmd when typeof(TResponse) == typeof(TokenResultDto):
                var restoreToken = await HandleGenerateRestoreTokenAsync(restoreTokenCmd, cancellationToken);
                return (true, (TResponse)(object)restoreToken);

            case InviteUserCommand inviteCmd when typeof(TResponse) == typeof(TokenResultDto):
                var inviteToken = await HandleInviteUserAsync(inviteCmd, cancellationToken);
                return (true, (TResponse)(object)inviteToken);

            case CompleteRegistrationCommand completeRegCmd when typeof(TResponse) == typeof(AuthTokensDto):
                var tokens = await HandleCompleteRegistrationAsync(completeRegCmd, cancellationToken);
                return (true, (TResponse)(object)tokens);

            default:
                return (false, default!);
        }
    }

    private async Task HandleUpdatePasswordAsync(UpdatePasswordCommand command, CancellationToken cancellationToken)
    {
        await ValidateCommandAsync(command, cancellationToken);

        var authUserRepository = serviceProvider.GetRequiredService<IAuthUserRepository>();
        var identityService = serviceProvider.GetRequiredService<IIdentityService>();

        var user = await authUserRepository.GetAsync<UserBasicDto>(command.UserId, cancellationToken);
        bool isCurrentPasswordValid = await identityService.CheckPasswordAsync(
            user.Email,
            command.CurrentPassword,
            cancellationToken);

        if (!isCurrentPasswordValid)
        {
            throw new UserOperationException(UserOperation.ChangePassword, "Incorrect password.");
        }

        LogDemoCommandIntercepted(logger, nameof(UpdatePasswordCommand));
    }

    private async Task HandleDeleteUserAsync(DeleteUserCommand command, CancellationToken cancellationToken)
    {
        await ValidateCommandAsync(command, cancellationToken);

        var roleRepository = serviceProvider.GetRequiredService<IAuthRoleRepository>();
        var authUserRepository = serviceProvider.GetRequiredService<IAuthUserRepository>();

        if (await roleRepository.IsUserLastAdminAsync(command.UserId, cancellationToken))
        {
            throw new InvalidOperationException("Cannot delete the user because they are the last system administrator.");
        }

        await authUserRepository.EnsureExistsAsync(command.UserId, cancellationToken);

        LogDemoCommandIntercepted(logger, nameof(DeleteUserCommand));
    }

    private async Task HandleResetPasswordAsync(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        await ValidateCommandAsync(command, cancellationToken);

        var tokenService = serviceProvider.GetRequiredService<ITokenService>();
        var authUserRepository = serviceProvider.GetRequiredService<IAuthUserRepository>();

        var principal = await tokenService.ValidateRestorePasswordTokenAsync(command.Token)
            ?? throw new InvalidCredentialException("The password reset token is invalid or has expired.");

        var userIdString = principal.FindFirst(CustomClaimTypes.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var rawResetToken = principal.FindFirst(CustomClaimTypes.PasswordResetToken)?.Value;

        if (!Guid.TryParse(userIdString, out var userId) || string.IsNullOrWhiteSpace(rawResetToken))
        {
            throw new InvalidCredentialException("The token does not contain valid user information or reset token.");
        }

        await authUserRepository.EnsureExistsAsync(userId, cancellationToken);

        LogDemoCommandIntercepted(logger, nameof(ResetPasswordCommand));
    }

    private async Task<TokenResultDto> HandleGenerateRestoreTokenAsync(
        GenerateRestorePasswordTokenCommand command,
        CancellationToken cancellationToken)
    {
        await ValidateCommandAsync(command, cancellationToken);

        var authUserRepository = serviceProvider.GetRequiredService<IAuthUserRepository>();
        await authUserRepository.EnsureExistsAsync(command.UserId, cancellationToken);

        int expiryHours = jwtOptions?.Value.ResetPasswordTokenExpiryHours is > 0
            ? jwtOptions.Value.ResetPasswordTokenExpiryHours
            : DefaultResetPasswordExpiryHours;

        LogDemoCommandIntercepted(logger, nameof(GenerateRestorePasswordTokenCommand));

        return new TokenResultDto(
            $"demo-reset-token-{Guid.NewGuid():N}",
            timeProvider.GetUtcNow().UtcDateTime.AddHours(expiryHours));
    }

    private async Task<TokenResultDto> HandleInviteUserAsync(
        InviteUserCommand command,
        CancellationToken cancellationToken)
    {
        await ValidateCommandAsync(command, cancellationToken);

        var authUserRepository = serviceProvider.GetRequiredService<IAuthUserRepository>();
        var ngoRepository = serviceProvider.GetRequiredService<INgoRepository>();
        var roleRepository = serviceProvider.GetRequiredService<IAuthRoleRepository>();

        await authUserRepository.EnsureNotExistsAsync(command.Email, cancellationToken);
        _ = await ngoRepository.GetIdAsync(cancellationToken);
        await roleRepository.EnsureAllExistAsync(command.RoleIds, cancellationToken);

        int expiryHours = jwtOptions?.Value.InvitationTokenExpiryHours is > 0
            ? jwtOptions.Value.InvitationTokenExpiryHours
            : DefaultInvitationExpiryHours;

        LogDemoCommandIntercepted(logger, nameof(InviteUserCommand));

        return new TokenResultDto(
            $"demo-invite-token-{Guid.NewGuid():N}",
            timeProvider.GetUtcNow().UtcDateTime.AddHours(expiryHours));
    }

    private async Task<AuthTokensDto> HandleCompleteRegistrationAsync(
        CompleteRegistrationCommand command,
        CancellationToken cancellationToken)
    {
        await ValidateCommandAsync(command, cancellationToken);

        var tokenService = serviceProvider.GetRequiredService<ITokenService>();
        var authUserRepository = serviceProvider.GetRequiredService<IAuthUserRepository>();

        var principal = await tokenService.ValidateInvitationTokenAsync(command.Token)
            ?? throw new InvalidOperationException("Invalid or expired invitation token.");

        var email = principal.FindFirst(ClaimTypes.Email)?.Value
            ?? throw new InvalidOperationException("Email claim is missing from the token.");

        await authUserRepository.EnsureNotExistsAsync(email, cancellationToken);

        LogDemoCommandIntercepted(logger, nameof(CompleteRegistrationCommand));

        return new AuthTokensDto(
            $"demo-registration-disabled-{Guid.NewGuid():N}",
            $"demo-registration-disabled-{Guid.NewGuid():N}");
    }

    private async Task ValidateCommandAsync<TCommand>(TCommand command, CancellationToken cancellationToken)
    {
        var validators = serviceProvider.GetServices<IValidator<TCommand>>().ToList();
        if (validators.Count == 0)
        {
            return;
        }

        var context = new ValidationContext<TCommand>(command);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = results
            .SelectMany(r => r.Errors)
            .Where(static f => f is not null)
            .ToList();

        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }
    }

    [LoggerMessage(
        EventId = DemoLogEventIds.DemoCommandIntercepted,
        Level = LogLevel.Information,
        Message = "Demo mode safely intercepted sensitive command '{CommandName}' without database mutation or real token issuance.")]
    private static partial void LogDemoCommandIntercepted(ILogger logger, string commandName);
}
