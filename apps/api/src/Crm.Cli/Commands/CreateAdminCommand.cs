using Crm.Application.Exceptions;
using Crm.Application.Features.Users.Commands;
using Crm.Application.Interfaces;
using Crm.Cli.Abstractions;
using Crm.Cli.Constants;
using Crm.Cli.Models;
using Crm.Cli.Services;
using Crm.Domain.Exceptions;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Cli.Commands;

/// <summary>
/// Creates the initial system administrator directly from the CLI before any user exists in the database.
/// Enforces the same rules as <see cref="CreateUserCommand"/> and <c>CreateUserCommandHandler</c>.
/// </summary>
public partial class CreateAdminCommand(
    ICommandHandler<CreateUserCommand, Guid> createUserHandler,
    IValidator<CreateUserCommand> validator,
    IUnitOfWork unitOfWork,
    IOptionService optionService,
    ICliConsole cliConsole,
    ILogger<CreateAdminCommand> logger) : ICliCommand
{
    private const string EmailOption = "email";
    private const string EmailShort = "e";
    private const string FirstNameOption = "first-name";
    private const string FirstNameShort = "f";
    private const string LastNameOption = "last-name";
    private const string LastNameShort = "l";
    private const string PatronymicOption = "patronymic";
    private const string PatronymicShort = "p";
    private const string PasswordStdinFlag = "password-stdin";

    /// <inheritdoc />
    public string Name => "create-admin";

    /// <inheritdoc />
    public string Description =>
        "Creates the first administrator (--email, --first-name, --last-name, [--patronymic], [--password-stdin]).";

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CliArguments args, CancellationToken cancellationToken = default)
    {
        if (!TryExtractProfileArguments(args, out var firstName, out var lastName, out var patronymic, out var email))
        {
            return 1;
        }

        var (passwordObtained, password, wasGenerated) = await ResolvePasswordAsync(args, cancellationToken);
        if (!passwordObtained || password is null)
        {
            return 1;
        }

        var command = new CreateUserCommand(
            FirstName: firstName,
            LastName: lastName,
            Patronymic: patronymic,
            Email: email,
            Password: password,
            ConfirmPassword: password);

        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var reason = string.Join(" ", validationResult.Errors.Select(e => e.ErrorMessage));
            LogValidationFailed(logger, reason);
            return 1;
        }

        return await CreateAdministratorAsync(command, wasGenerated, cancellationToken);
    }

    private bool TryExtractProfileArguments(
        CliArguments args,
        out string firstName,
        out string lastName,
        out string? patronymic,
        out string email)
    {
        email = args.GetValue(EmailOption, EmailShort) ?? string.Empty;
        firstName = args.GetValue(FirstNameOption, FirstNameShort) ?? string.Empty;
        lastName = args.GetValue(LastNameOption, LastNameShort) ?? string.Empty;

        var rawPatronymic = args.GetValue(PatronymicOption, PatronymicShort);
        patronymic = string.IsNullOrWhiteSpace(rawPatronymic) ? null : rawPatronymic;

        var missingOptions = new List<string>(3);

        if (string.IsNullOrWhiteSpace(email))
        {
            missingOptions.Add("--email (-e)");
        }

        if (string.IsNullOrWhiteSpace(firstName))
        {
            missingOptions.Add("--first-name (-f)");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            missingOptions.Add("--last-name (-l)");
        }

        if (missingOptions.Count > 0)
        {
            LogMissingArguments(logger, string.Join(", ", missingOptions));
            return false;
        }

        return true;
    }

    private async Task<(bool Success, string? Password, bool WasGenerated)> ResolvePasswordAsync(
        CliArguments args,
        CancellationToken cancellationToken)
    {
        if (args.HasFlag(PasswordStdinFlag))
        {
            var stdinPassword = await cliConsole.In.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(stdinPassword))
            {
                LogValidationFailed(logger, "Password read from standard input cannot be empty.");
                return (false, null, false);
            }

            return (true, stdinPassword, false);
        }

        var generated = CompliantPasswordGenerator.Generate(optionService.GetIdentityOptions());
        return (true, generated, true);
    }

    private async Task<int> CreateAdministratorAsync(
        CreateUserCommand command,
        bool wasGeneratedPassword,
        CancellationToken cancellationToken)
    {
        bool transactionStarted = false;

        try
        {
            await unitOfWork.BeginTransactionAsync(cancellationToken);
            transactionStarted = true;

            var userId = await createUserHandler.Handle(command, cancellationToken);

            await unitOfWork.CommitTransactionAsync(cancellationToken);

            LogAdminCreated(logger, command.Email, userId);

            if (wasGeneratedPassword)
            {
                await cliConsole.Out.WriteLineAsync($"Generated password: {command.Password}");
            }

            return 0;
        }
        catch (EntityAlreadyExistsException)
        {
            await TryRollbackAsync(transactionStarted);
            LogUsersAlreadyExist(logger);
            return 1;
        }
        catch (UserOperationException ex)
        {
            await TryRollbackAsync(transactionStarted);
            LogOperationFailed(logger, command.Email, ex.Message);
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            await TryRollbackAsync(transactionStarted);
            LogOperationFailed(logger, command.Email, ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            await TryRollbackAsync(transactionStarted);
            LogUnexpectedError(logger, command.Email, ex);
            return 1;
        }
    }

    private async Task TryRollbackAsync(bool transactionStarted)
    {
        if (transactionStarted)
        {
            await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
        }
    }

    [LoggerMessage(EventId = CliLogEventIds.CreateAdminMissingArguments, Level = LogLevel.Error, Message = "Missing required option(s): {MissingOptions}.")]
    private static partial void LogMissingArguments(ILogger logger, string missingOptions);

    [LoggerMessage(EventId = CliLogEventIds.CreateAdminValidationFailed, Level = LogLevel.Error, Message = "Administrator creation rejected due to validation error(s): {Reason}")]
    private static partial void LogValidationFailed(ILogger logger, string reason);

    [LoggerMessage(EventId = CliLogEventIds.CreateAdminUsersAlreadyExist, Level = LogLevel.Error, Message = "Cannot create administrator: at least one user already exists in the database.")]
    private static partial void LogUsersAlreadyExist(ILogger logger);

    [LoggerMessage(EventId = CliLogEventIds.CreateAdminOperationFailed, Level = LogLevel.Error, Message = "Failed to create administrator '{Email}': {Reason}")]
    private static partial void LogOperationFailed(ILogger logger, string email, string reason);

    [LoggerMessage(EventId = CliLogEventIds.CreateAdminUnexpectedError, Level = LogLevel.Error, Message = "An unexpected error occurred while creating administrator '{Email}'.")]
    private static partial void LogUnexpectedError(ILogger logger, string email, Exception exception);

    [LoggerMessage(EventId = CliLogEventIds.CreateAdminSuccess, Level = LogLevel.Information, Message = "Administrator '{Email}' was successfully created with ID '{UserId}'.")]
    private static partial void LogAdminCreated(ILogger logger, string email, Guid userId);
}
