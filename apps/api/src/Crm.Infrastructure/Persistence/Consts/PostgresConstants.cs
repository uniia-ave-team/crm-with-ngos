namespace Crm.Infrastructure.Persistence.Consts;

/// <summary>
/// Provides PostgreSQL store-type and index-method constants used across EF Core entity configurations
/// and custom database function translations.
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
    /// PostgreSQL timezone-naive timestamp store type (<c>timestamp without time zone</c>).
    /// </summary>
    public const string TimestampWithoutTimeZone = "timestamp without time zone";
}
