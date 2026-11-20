using System.Data.Common;
using System.Text.RegularExpressions;
using Crm.Api.DemoMode.Sandbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Crm.Api.DemoMode;

/// <summary>
/// Fail-safe for demo mode: an HTTP request that has no sandbox works with the shared demo database
/// (the source of every sandbox) and must never change it. Startup migrations, seeders and background
/// services run outside HTTP requests and are not affected.
/// </summary>
/// <param name="demoModeState">The demo mode state.</param>
/// <param name="sandboxAccessor">The accessor of the current sandbox.</param>
/// <param name="logger">The logger.</param>
public sealed partial class DemoDbWriteGuardInterceptor(
    IDemoModeState demoModeState,
    IDemoSandboxAccessor sandboxAccessor,
    ILogger<DemoDbWriteGuardInterceptor> logger) : ISaveChangesInterceptor, IDbCommandInterceptor
{
    /// <inheritdoc />
    public InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        GuardSaveChanges(eventData.Context);
        return result;
    }

    /// <inheritdoc />
    public ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
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

    /// <summary>
    /// Checks whether a SQL command changes data or schema.
    /// </summary>
    /// <param name="commandText">The SQL text.</param>
    /// <returns><c>true</c> when the command is mutating.</returns>
    internal static bool IsMutatingSql(string? commandText)
        => !string.IsNullOrWhiteSpace(commandText) && MutatingSqlRegex().IsMatch(commandText);

    [GeneratedRegex(
        @"(?im)(?:^|[;\s])(?:INSERT|UPDATE|DELETE|TRUNCATE|MERGE|CREATE|ALTER|DROP|GRANT|REVOKE)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex MutatingSqlRegex();

    [LoggerMessage(
        EventId = DemoLogEventIds.UnguardedDbWriteBlocked,
        Level = LogLevel.Warning,
        Message = "Demo mode blocked a database write ({Operation}) from a request without a sandbox.")]
    private static partial void LogUnguardedDbWriteBlocked(ILogger logger, string operation);

    private bool ShouldBlockWrites() =>
        demoModeState.IsActive &&
        sandboxAccessor.IsHttpRequest &&
        sandboxAccessor.Current is null;

    private void GuardSaveChanges(DbContext? context)
    {
        if (context is null || !ShouldBlockWrites() || !context.ChangeTracker.HasChanges())
        {
            return;
        }

        LogUnguardedDbWriteBlocked(logger, "SaveChanges");

        throw new DemoModeException();
    }

    private void GuardCommand(DbCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!ShouldBlockWrites() || !IsMutatingSql(command.CommandText))
        {
            return;
        }

        LogUnguardedDbWriteBlocked(logger, "DbCommand");

        throw new DemoModeException();
    }
}
