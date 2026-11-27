namespace Crm.Infrastructure.Persistence.Sorting;

/// <summary>
/// Internal sorting classification inferred from the string values of a dynamic JSONB column.
/// Keeps smart ordering isolated inside the infrastructure query layer without coupling domain schemas to data types.
/// </summary>
public enum InferredSortKind : byte
{
    /// <summary>
    /// General text sorted using PostgreSQL natural Ukrainian ICU collation (<c>uk-u-kn-true</c>).
    /// </summary>
    Text = 0,

    /// <summary>
    /// Numeric values (integers, negatives, decimals) sorted numerically with natural text fallback.
    /// </summary>
    Number = 1,

    /// <summary>
    /// Boolean values (<c>true</c>/<c>false</c>) sorted as PostgreSQL booleans with natural text fallback.
    /// </summary>
    Boolean = 2,

    /// <summary>
    /// ISO 8601 calendar dates (<c>yyyy-MM-dd</c>) sorted chronologically with natural text fallback.
    /// </summary>
    Date = 3,

    /// <summary>
    /// ISO 8601 timestamps sorted chronologically in UTC with natural text fallback.
    /// </summary>
    DateTime = 4,
}
