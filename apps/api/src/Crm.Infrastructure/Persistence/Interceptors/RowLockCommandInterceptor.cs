using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Crm.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Intercepts EF Core SQL commands tagged with <see cref="ForUpdateTag"/> and appends a
/// row-level <c>FOR UPDATE</c> lock clause without requiring raw SQL in repositories.
/// </summary>
public sealed class RowLockCommandInterceptor : DbCommandInterceptor
{
    /// <summary>
    /// The EF Core query tag marker that instructs this interceptor to append <c>FOR UPDATE</c>.
    /// </summary>
    internal const string ForUpdateTag = "__ROW_LOCK_FOR_UPDATE__";

    private const string TagComment = $"-- {ForUpdateTag}";
    private const string ForUpdateClause = "FOR UPDATE";

    /// <inheritdoc />
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        ApplyRowLockIfTagged(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        ApplyRowLockIfTagged(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    private static void ApplyRowLockIfTagged(DbCommand command)
    {
        if (string.IsNullOrEmpty(command.CommandText)
            || !command.CommandText.Contains(TagComment, StringComparison.Ordinal))
        {
            return;
        }

        string trimmed = command.CommandText.TrimEnd();
        bool hasTrailingSemicolon = trimmed.EndsWith(';');
        string sqlWithoutSemicolon = hasTrailingSemicolon ? trimmed[..^1].TrimEnd() : trimmed;

        if (sqlWithoutSemicolon.EndsWith(ForUpdateClause, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        command.CommandText = hasTrailingSemicolon
            ? $"{sqlWithoutSemicolon}\n{ForUpdateClause};"
            : $"{sqlWithoutSemicolon}\n{ForUpdateClause}";
    }
}
