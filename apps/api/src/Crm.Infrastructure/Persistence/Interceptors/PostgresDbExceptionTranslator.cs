using Crm.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Crm.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Translates PostgreSQL and EF Core persistence exceptions into domain conflict exceptions
/// without leaking database-specific details to upper layers.
/// </summary>
public sealed class PostgresDbExceptionTranslator : IDbExceptionTranslator
{
    internal const string UniqueViolationMessage = "A resource with the same unique value already exists.";
    internal const string ForeignKeyViolationMessage = "The operation could not be completed because a referenced or dependent resource does not exist or is still in use.";
    internal const string ConcurrencyConflictMessage = "The resource was modified or deleted concurrently.";

    /// <inheritdoc />
    public Exception? Translate(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        var queue = new Queue<Exception>();
        queue.Enqueue(exception);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
            {
                continue;
            }

            if (current is ConflictException existingConflict)
            {
                return existingConflict;
            }

            if (current is DbUpdateConcurrencyException)
            {
                return new ConcurrencyConflictException(ConcurrencyConflictMessage, exception);
            }

            if (current is PostgresException postgresException)
            {
                var translated = TranslatePostgresException(postgresException, exception);
                if (translated is not null)
                {
                    return translated;
                }
            }

            if (current is AggregateException aggregateException)
            {
                foreach (var inner in aggregateException.InnerExceptions)
                {
                    queue.Enqueue(inner);
                }
            }
            else if (current.InnerException is not null)
            {
                queue.Enqueue(current.InnerException);
            }
        }

        return null;
    }

    private static ConflictException? TranslatePostgresException(PostgresException postgresException, Exception originalException)
        => postgresException.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation => new ConflictException(
                UniqueViolationMessage,
                ConflictKind.UniqueViolation,
                originalException),
            PostgresErrorCodes.ForeignKeyViolation => new ConflictException(
                ForeignKeyViolationMessage,
                ConflictKind.ReferenceViolation,
                originalException),
            _ => null,
        };
}
