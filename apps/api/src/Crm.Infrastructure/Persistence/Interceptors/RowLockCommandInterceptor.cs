using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Crm.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Intercepts EF Core SQL commands tagged with <see cref="ForNoKeyUpdateTag"/> and appends a
/// row-level <c>FOR NO KEY UPDATE</c> lock clause without requiring raw SQL in repositories.
/// </summary>
public sealed class RowLockCommandInterceptor : DbCommandInterceptor
{
    /// <summary>
    /// The EF Core query tag marker that instructs this interceptor to append <c>FOR NO KEY UPDATE</c>.
    /// </summary>
    internal const string ForNoKeyUpdateTag = "__ROW_LOCK_FOR_NO_KEY_UPDATE__";

    private const string TagComment = $"-- {ForNoKeyUpdateTag}";
    private const string ForNoKeyUpdateClause = "FOR NO KEY UPDATE";

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

        if (sqlWithoutSemicolon.EndsWith(ForNoKeyUpdateClause, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        command.CommandText = hasTrailingSemicolon
            ? $"{sqlWithoutSemicolon}\n{ForNoKeyUpdateClause};"
            : $"{sqlWithoutSemicolon}\n{ForNoKeyUpdateClause}";
    }
}
