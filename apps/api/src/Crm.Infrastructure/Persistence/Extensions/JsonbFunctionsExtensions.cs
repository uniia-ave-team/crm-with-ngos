using System.Text.Json;
using System.Text.Json.Nodes;

namespace Crm.Infrastructure.Persistence.Extensions;

/// <summary>
/// Declares EF Core database-function stubs for PostgreSQL <c>jsonb</c> text extraction and schema key synchronization.
/// </summary>
public static class JsonbFunctionsExtensions
{
    private const string ExceptionMessage = "This method is for EF Core LINQ queries only.";

    /// <summary>
    /// Extracts a scalar text value from a JSONB document column at the specified top-level property key.
    /// </summary>
    /// <param name="json">The target JSONB document column.</param>
    /// <param name="key">The top-level JSON property key.</param>
    /// <returns>The extracted text value (SQL translation only).</returns>
    public static string? JsonbExtractPathText(JsonObject json, string key)
        => throw new NotSupportedException(ExceptionMessage);

    /// <summary>
    /// Extracts a scalar text value from a parameterized JSONB <see cref="JsonDocument"/> at the specified top-level property key.
    /// </summary>
    /// <param name="json">The parameterized JSONB lookup document.</param>
    /// <param name="key">The top-level JSON property key.</param>
    /// <returns>The extracted text value (SQL translation only).</returns>
    public static string? JsonbExtractPathText(JsonDocument json, string key)
        => throw new NotSupportedException(ExceptionMessage);

    /// <summary>
    /// Atomically strips <paramref name="dropKeys"/> from <paramref name="json"/> and backfills any missing or null
    /// keys in <paramref name="backfillKeys"/> with empty strings (<c>""</c>) via the PostgreSQL <c>crm_sync_jsonb_keys</c> function.
    /// </summary>
    /// <param name="json">The target JSONB document column.</param>
    /// <param name="dropKeys">Removed field keys to strip from the document.</param>
    /// <param name="backfillKeys">Newly required field keys to backfill with <c>""</c> when missing or null.</param>
    /// <returns>The synchronized JSONB document (SQL translation only).</returns>
    public static JsonObject SyncJsonbKeys(JsonObject json, string[] dropKeys, string[] backfillKeys)
        => throw new NotSupportedException(ExceptionMessage);
}
