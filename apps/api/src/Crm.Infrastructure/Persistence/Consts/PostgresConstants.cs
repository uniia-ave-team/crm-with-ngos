namespace Crm.Infrastructure.Persistence.Consts;

/// <summary>
/// Provides PostgreSQL store-type, index-method, collation, and custom SQL function constants
/// used across EF Core entity configurations and database function registrations.
/// </summary>
internal static class PostgresConstants
{
    /// <summary>
    /// PostgreSQL binary JSON column store type (<c>jsonb</c>).
    /// </summary>
    public const string Jsonb = "jsonb";

    /// <summary>
    /// PostgreSQL Generalized Inverted Index method (<c>gin</c>).
    /// </summary>
    public const string Gin = "gin";

    /// <summary>
    /// PostgreSQL POSIX binary collation (<c>C</c>) matching C# <see cref="StringComparer.Ordinal"/>
    /// for language-independent ASCII fractional index ordering.
    /// </summary>
    public const string CCollation = "C";

    /// <summary>
    /// PostgreSQL text store type (<c>text</c>).
    /// </summary>
    public const string Text = "text";

    /// <summary>
    /// PostgreSQL text array store type (<c>text[]</c>).
    /// </summary>
    public const string TextArray = "text[]";

    /// <summary>
    /// PostgreSQL boolean store type (<c>boolean</c>).
    /// </summary>
    public const string Boolean = "boolean";

    /// <summary>
    /// PostgreSQL arbitrary-precision numeric store type (<c>numeric</c>).
    /// </summary>
    public const string Numeric = "numeric";

    /// <summary>
    /// PostgreSQL calendar date store type (<c>date</c>).
    /// </summary>
    public const string Date = "date";

    /// <summary>
    /// PostgreSQL UTC-aware timestamp store type (<c>timestamp with time zone</c>).
    /// </summary>
    public const string TimestampWithTimeZone = "timestamp with time zone";

    /// <summary>
    /// Built-in PostgreSQL function extracting a text value from a JSONB object at a top-level key.
    /// </summary>
    public const string JsonbExtractPathTextFunction = "jsonb_extract_path_text";

    /// <summary>
    /// Custom PostgreSQL function atomically dropping removed keys and backfilling newly required keys in JSONB payloads.
    /// </summary>
    public const string SyncJsonbKeysFunction = "crm_sync_jsonb_keys";

    /// <summary>
    /// Custom PostgreSQL function safely casting text to <c>numeric</c> (excluding NaN and Infinity).
    /// </summary>
    public const string TryNumericFunction = "crm_try_numeric";

    /// <summary>
    /// Custom PostgreSQL function safely casting text to <c>boolean</c>.
    /// </summary>
    public const string TryBooleanFunction = "crm_try_boolean";

    /// <summary>
    /// Custom PostgreSQL function safely casting text to UTC-normalized <c>date</c>.
    /// </summary>
    public const string TryDateFunction = "crm_try_date";

    /// <summary>
    /// Custom PostgreSQL function safely casting text to <c>timestamp with time zone</c> (excluding Infinity).
    /// </summary>
    public const string TryTimestampTzFunction = "crm_try_timestamptz";
}
