using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Crm.Api.DemoMode;

/// <summary>
/// Fail-safe EF Core interceptor that blocks any unguarded database write during an HTTP request
/// when Demo Mode is active unless the write runs inside a rolled-back dry-run transaction
/// or an explicitly allowed (<see cref="DemoExecutionMode.ExplicitlyAllowed"/>) scope.
/// Startup migrations and seeders (where <see cref="IHttpContextAccessor.HttpContext"/> is <see langword="null"/>)
/// are unaffected.
/// </summary>
/// <param name="demoModeState">The service providing current Demo Mode state.</param>
/// <param name="executionContext">The active Demo Mode execution context.</param>
/// <param name="httpContextAccessor">The HTTP context accessor used to distinguish HTTP requests from startup tasks.</param>
/// <param name="logger">The logger used to record blocked unguarded write attempts.</param>
public sealed partial class DemoDbWriteGuardInterceptor(
    IDemoModeState demoModeState,
    IDemoExecutionContext executionContext,
    IHttpContextAccessor httpContextAccessor,
    ILogger<DemoDbWriteGuardInterceptor> logger) : ISaveChangesInterceptor, IDbCommandInterceptor
{
    private const string BlockedSaveMessage = "Demo Mode blocked an unguarded database SaveChanges operation outside a dry-run transaction.";
    private const string BlockedSqlMessage = "Demo Mode blocked an unguarded mutating SQL command outside a dry-run transaction.";

    /// <inheritdoc />
    public InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        GuardSaveChanges(eventData.Context);
        return result;
    }

    /// <inheritdoc />
    public ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        GuardSaveChanges(eventData.Context);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        GuardCommand(command);
        return result;
    }

    /// <inheritdoc />
    public ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        GuardCommand(command);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        GuardCommand(command);
        return result;
    }

    /// <inheritdoc />
    public ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        GuardCommand(command);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        GuardCommand(command);
        return result;
    }

    /// <inheritdoc />
    public ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        GuardCommand(command);
        return ValueTask.FromResult(result);
    }

    internal static bool IsMutatingSql(string? commandText)
        => !string.IsNullOrWhiteSpace(commandText) && MutatingSqlRegex().IsMatch(commandText);

    private void GuardSaveChanges(DbContext? context)
    {
        if (!ShouldEnforceGuard() || context is null || !context.ChangeTracker.HasChanges())
        {
            return;
        }

        if (!executionContext.IsDryRunActive || context.Database.CurrentTransaction is null)
        {
            LogUnguardedDbWriteBlocked(logger, "SaveChanges", executionContext.Mode);
            throw new InvalidOperationException(BlockedSaveMessage);
        }
    }

    private void GuardCommand(DbCommand command)
    {
        if (!ShouldEnforceGuard() || !IsMutatingSql(command.CommandText))
        {
            return;
        }

        if (!executionContext.IsDryRunActive || command.Transaction is null)
        {
            LogUnguardedDbWriteBlocked(logger, "DbCommand", executionContext.Mode);
            throw new InvalidOperationException(BlockedSqlMessage);
        }
    }

    private bool ShouldEnforceGuard() =>
        demoModeState.IsActive &&
        httpContextAccessor.HttpContext is not null &&
        !executionContext.IsExplicitlyAllowed;

    [GeneratedRegex(
        @"(?im)(?:^|[;\s])(?:INSERT|UPDATE|DELETE|TRUNCATE|MERGE|CREATE|ALTER|DROP|GRANT|REVOKE)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex MutatingSqlRegex();

    [LoggerMessage(
        EventId = DemoLogEventIds.UnguardedDbWriteBlocked,
        Level = LogLevel.Warning,
        Message = "Demo mode fail-safe blocked an unguarded database write ({Operation}) in execution mode {Mode}.")]
    private static partial void LogUnguardedDbWriteBlocked(ILogger logger, string operation, DemoExecutionMode mode);
}
