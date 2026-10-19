using System.Diagnostics.CodeAnalysis;
using System.Text;
using Crm.Application.Exceptions;
using Crm.Application.Interfaces;
using Crm.Cli.Abstractions;
using Crm.Cli.Constants;
using Crm.Cli.Models;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Entities;
using Microsoft.Extensions.Logging;

namespace Crm.Cli.Commands;

/// <summary>
/// Handles the manual password reset for system administrators directly via the CLI interface.
/// Useful for disaster recovery, initial setup, or unlocking administrative accounts.
/// </summary>
/// <param name="identityService">The identity service used to manage user authentication and authorization.</param>
/// <param name="authUserRepository">The repository used to access and manage authentication user data.</param>
/// <param name="logger">The logger instance used for structured diagnostic output.</param>
public partial class ResetAdminPasswordCommand(
    IIdentityService identityService,
    IAuthUserRepository authUserRepository,
    ILogger<ResetAdminPasswordCommand> logger) : ICliCommand
{
    private const string Email = "email";
    private const string EmailShort = "e";

    /// <inheritdoc />
    public string Name => "reset-password";

    /// <inheritdoc />
    public string Description => "Resets the password for the administrator matching the specified email (--email).";

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CliArguments args, CancellationToken cancellationToken = default)
    {
        if (!TryGetEmail(args, out var email))
        {
            return 1;
        }

        var admin = await FindAdminAsync(email, cancellationToken);

        return admin is not null
            && TryReadPasswordSecurely(out var password)
            && await TryResetPasswordAsync(admin, password, cancellationToken)
            ? 0 : 1;
    }

    private bool TryGetEmail(
        CliArguments args,
        [NotNullWhen(true)] out string? email)
    {
        email = args.GetValue(Email, EmailShort);

        if (string.IsNullOrWhiteSpace(email))
        {
            LogEmailMissing(logger);
            return false;
        }

        return true;
    }

    private bool TryReadPasswordSecurely([NotNullWhen(true)] out string? password)
    {
        var newPassword = ReadHiddenPassword("Enter new password: ");

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            LogPasswordMissing(logger);
            password = null;
            return false;
        }

        var confirmPassword = ReadHiddenPassword("Confirm new password: ");

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            LogPasswordsDoNotMatch(logger);
            password = null;
            return false;
        }

        password = newPassword;
        return true;
    }

    private static string ReadHiddenPassword(string prompt)
    {
        Console.Write(prompt);

        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? string.Empty;
        }

        var password = new StringBuilder();

        while (true)
        {
            var keyInfo = Console.ReadKey(intercept: true);

            if (keyInfo.Key is ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (keyInfo.Key is ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Remove(password.Length - 1, 1);
                }
            }
            else if (!char.IsControl(keyInfo.KeyChar))
            {
                password.Append(keyInfo.KeyChar);
            }
        }

        return password.ToString();
    }

    private async Task<AuthUser?> FindAdminAsync(string email, CancellationToken cancellationToken)
    {
        try
        {
            return await authUserRepository.GetAsync<AuthUser>(email, cancellationToken);
        }
        catch (EntityNotFoundException ex)
        {
            LogAdminNotFound(logger, email, ex);
            return null;
        }
        catch (Exception ex)
        {
            LogUserRetrievalFailed(logger, email, ex);
            return null;
        }
    }

    private async Task<bool> TryResetPasswordAsync(AuthUser admin, string newPassword, CancellationToken cancellationToken)
    {
        var identifier = admin.Email ?? admin.Id.ToString();

        try
        {
            await identityService.ResetPasswordAsync(admin.Id, newPassword, cancellationToken);
            LogPasswordResetSuccess(logger, identifier);
            return true;
        }
        catch (UserOperationException ex)
        {
            LogPasswordResetFailed(logger, identifier, ex);
            return false;
        }
    }

    [LoggerMessage(EventId = CliLogEventIds.PasswordArgumentMissing, Level = LogLevel.Warning, Message = "Password cannot be empty.")]
    private static partial void LogPasswordMissing(ILogger logger);

    [LoggerMessage(EventId = CliLogEventIds.PasswordsDoNotMatch, Level = LogLevel.Warning, Message = "The entered passwords do not match.")]
    private static partial void LogPasswordsDoNotMatch(ILogger logger);

    [LoggerMessage(EventId = CliLogEventIds.EmailArgumentMissing, Level = LogLevel.Warning, Message = "An email must be provided. Use --email <value> or -e <value>.")]
    private static partial void LogEmailMissing(ILogger logger);

    [LoggerMessage(EventId = CliLogEventIds.AdminUserNotFound, Level = LogLevel.Error, Message = "Target administrator user with email '{Email}' could not be found in the database.")]
    private static partial void LogAdminNotFound(ILogger logger, string email, Exception exception);

    [LoggerMessage(EventId = CliLogEventIds.UserRetrievalFailed, Level = LogLevel.Error, Message = "An unexpected error occurred while retrieving administrator user '{Email}'.")]
    private static partial void LogUserRetrievalFailed(ILogger logger, string email, Exception exception);

    [LoggerMessage(EventId = CliLogEventIds.PasswordResetFailed, Level = LogLevel.Error, Message = "An unexpected error occurred while resetting the administrator password for user '{UserIdentifier}'.")]
    private static partial void LogPasswordResetFailed(ILogger logger, string userIdentifier, Exception exception);

    [LoggerMessage(EventId = CliLogEventIds.PasswordResetSuccess, Level = LogLevel.Information, Message = "Password for user '{UserIdentifier}' was successfully updated.")]
    private static partial void LogPasswordResetSuccess(ILogger logger, string userIdentifier);
}
