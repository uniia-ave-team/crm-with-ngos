using System.Globalization;
using System.Linq.Expressions;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Sorting;

/// <summary>
/// Infers the most accurate <see cref="InferredSortKind"/> for a string-valued JSONB column
/// by inspecting a deterministically ordered bounded sample of non-empty values in plain C#.
/// </summary>
public static class ColumnSortKindDetector
{
    /// <summary>
    /// Maximum number of non-empty column values sampled to infer the dominant sort kind.
    /// </summary>
    public const int SampleSize = 200;

    /// <summary>
    /// Minimum percentage (0–100) of non-empty sampled values that must match a typed format
    /// before enabling hybrid typed SQL casting for the column.
    /// </summary>
    public const int DominanceThresholdPercent = 80;

    private const string IsoDateFormat = "yyyy-MM-dd";

    private const NumberStyles PostgresNumericStyles =
        NumberStyles.AllowLeadingSign
        | NumberStyles.AllowDecimalPoint
        | NumberStyles.AllowExponent;

    /// <summary>
    /// Builds a deterministic SQL-translatable sample query selecting up to <see cref="SampleSize"/>
    /// non-empty column values from a deterministically ordered source sequence.
    /// </summary>
    /// <typeparam name="TEntity">The queried entity type.</typeparam>
    /// <param name="orderedQuery">The deterministically ordered source queryable sequence.</param>
    /// <param name="textSelector">The SQL-translatable expression extracting the text value for the target column.</param>
    /// <returns>The bounded sample query preserving the caller's ordering before <c>LIMIT</c>.</returns>
    public static IQueryable<string?> BuildSampleQuery<TEntity>(
        this IOrderedQueryable<TEntity> orderedQuery,
        Expression<Func<TEntity, string?>> textSelector)
    {
        ArgumentNullException.ThrowIfNull(orderedQuery);
        ArgumentNullException.ThrowIfNull(textSelector);

        Expression<Func<TEntity, bool>> hasNonEmptyValue = e =>
            textSelector.Invoke(e) != null && textSelector.Invoke(e) != string.Empty;

        return orderedQuery
            .Where(hasNonEmptyValue.Expand())
            .Select(textSelector)
            .Take(SampleSize);
    }

    /// <summary>
    /// Deterministically samples up to <see cref="SampleSize"/> non-empty values from <paramref name="orderedQuery"/>
    /// using <paramref name="textSelector"/> and returns the inferred <see cref="InferredSortKind"/>.
    /// </summary>
    /// <typeparam name="TEntity">The queried entity type.</typeparam>
    /// <param name="orderedQuery">The deterministically ordered source queryable sequence.</param>
    /// <param name="textSelector">The SQL-translatable expression extracting the text value for the target column.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The inferred <see cref="InferredSortKind"/> for the column.</returns>
    public static async Task<InferredSortKind> DetectSortKindAsync<TEntity>(
        this IOrderedQueryable<TEntity> orderedQuery,
        Expression<Func<TEntity, string?>> textSelector,
        CancellationToken cancellationToken = default)
    {
        List<string?> sample = await orderedQuery
            .BuildSampleQuery(textSelector)
            .ToListAsync(cancellationToken);

        return Detect(sample);
    }

    /// <summary>
    /// Classifies a sample of raw column values into the best-matching <see cref="InferredSortKind"/>.
    /// </summary>
    /// <param name="sample">The sampled raw string values.</param>
    /// <returns>The dominant <see cref="InferredSortKind"/> if at least <see cref="DominanceThresholdPercent"/>% match; otherwise <see cref="InferredSortKind.Text"/>.</returns>
    public static InferredSortKind Detect(IReadOnlyList<string?> sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        int total = 0;
        int booleanCount = 0;
        int numberCount = 0;
        int dateCount = 0;
        int dateTimeCount = 0;

        for (int i = 0; i < sample.Count; i++)
        {
            string? raw = sample[i];
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            ReadOnlySpan<char> trimmed = raw.AsSpan().Trim();
            total++;

            if (bool.TryParse(trimmed, out _))
            {
                booleanCount++;
            }
            else if (decimal.TryParse(trimmed, PostgresNumericStyles, CultureInfo.InvariantCulture, out _))
            {
                numberCount++;
            }
            else if (IsIsoDate(trimmed))
            {
                dateCount++;
            }
            else if (IsIsoDateTime(trimmed))
            {
                dateTimeCount++;
            }
        }

        return total == 0
            ? InferredSortKind.Text
            : MeetsThreshold(booleanCount, total)
                ? InferredSortKind.Boolean
                : MeetsThreshold(numberCount, total)
                    ? InferredSortKind.Number
                    : MeetsThreshold(dateCount, total)
                        ? InferredSortKind.Date
                        : MeetsThreshold(dateTimeCount, total)
                            ? InferredSortKind.DateTime
                            : InferredSortKind.Text;
    }

    private static bool MeetsThreshold(int matchCount, int total)
        => matchCount * 100 >= total * DominanceThresholdPercent;

    private static bool IsIsoDate(ReadOnlySpan<char> value)
        => value.Length == IsoDateFormat.Length
            && DateOnly.TryParseExact(value, IsoDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static bool IsIsoDateTime(ReadOnlySpan<char> value)
        => (value.Contains('T') || value.Contains('t'))
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _);
}
