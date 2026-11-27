namespace Crm.Infrastructure.Persistence.Extensions;

/// <summary>
/// Declares EF Core database-function stubs for safe PostgreSQL typed casting of text values
/// (reusing the same <c>pg_input_is_valid</c>, <c>regexp_like</c>, and UTC date normalization translations
/// as JSONB extraction in <see cref="ModelBuilderDbFunctionsExtensions"/>).
/// </summary>
public static class PostgresCastFunctions
{
    private const string ExceptionMessage = "This method is for EF Core LINQ queries only.";

    /// <summary>
    /// Safely casts a text value to PostgreSQL <c>numeric</c>, returning <see langword="null"/> for invalid or special values.
    /// </summary>
    /// <param name="value">The text value to cast.</param>
    /// <returns>The cast numeric value, or <see langword="null"/> (SQL translation only).</returns>
    public static decimal? SafeCastNumber(string? value)
        => throw new NotSupportedException(ExceptionMessage);

    /// <summary>
    /// Safely casts a text value to PostgreSQL <c>boolean</c>, returning <see langword="null"/> for invalid values.
    /// </summary>
    /// <param name="value">The text value to cast.</param>
    /// <returns>The cast boolean value, or <see langword="null"/> (SQL translation only).</returns>
    public static bool? SafeCastBoolean(string? value)
        => throw new NotSupportedException(ExceptionMessage);

    /// <summary>
    /// Safely casts a text value to a UTC-normalized PostgreSQL <c>date</c>, returning <see langword="null"/> for invalid values.
    /// </summary>
    /// <param name="value">The text value to cast.</param>
    /// <returns>The cast calendar date value, or <see langword="null"/> (SQL translation only).</returns>
    public static DateOnly? SafeCastDate(string? value)
        => throw new NotSupportedException(ExceptionMessage);

    /// <summary>
    /// Safely casts a text value to PostgreSQL <c>timestamp with time zone</c>, returning <see langword="null"/> for invalid values.
    /// </summary>
    /// <param name="value">The text value to cast.</param>
    /// <returns>The cast UTC timestamp value, or <see langword="null"/> (SQL translation only).</returns>
    public static DateTime? SafeCastDateTime(string? value)
        => throw new NotSupportedException(ExceptionMessage);
}
