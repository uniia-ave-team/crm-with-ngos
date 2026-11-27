using System.Text;

namespace Crm.Domain.Common;

/// <summary>
/// Implements lexicographical fractional indexing (base-62 ASCII ordinal order) for manual row positioning
/// inside custom tables. All generated ranks strictly satisfy <see cref="StringComparer.Ordinal"/> ordering
/// (matching PostgreSQL <c>COLLATE "C"</c>) and stay compact across millions of sequential appends/prepends.
/// </summary>
public static class FractionalIndex
{
    /// <summary>
    /// Maximum rank string length stored in the database column.
    /// </summary>
    public const int MaxColumnLength = 256;

    /// <summary>
    /// Threshold at which a table's ranks are automatically rebalanced to compact keys.
    /// </summary>
    public const int RebalanceThresholdLength = 200;

    /// <summary>
    /// Initial rank assigned to the first row inserted into an empty table.
    /// </summary>
    public const string Initial = "a0";

    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int Base = 62;
    private static readonly char MidChar = Alphabet[Base / 2];

    /// <summary>
    /// Computes a rank strictly between <paramref name="prev"/> and <paramref name="next"/>.
    /// </summary>
    /// <param name="prev">The preceding row's rank, or <see langword="null"/> when placing at the start.</param>
    /// <param name="next">The following row's rank, or <see langword="null"/> when placing at the end.</param>
    /// <returns>A rank strictly greater than <paramref name="prev"/> and strictly less than <paramref name="next"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="prev"/> is greater than or equal to <paramref name="next"/>.</exception>
    public static string Between(string? prev, string? next)
        => (string.IsNullOrEmpty(prev), string.IsNullOrEmpty(next)) switch
        {
            (true, true) => Initial,
            (true, false) => Before(next!),
            (false, true) => After(prev!),
            (false, false) => string.CompareOrdinal(prev, next) >= 0
                ? throw new ArgumentException($"Previous rank '{prev}' must be strictly less than next rank '{next}'.")
                : Midpoint(prev!, next),
        };

    /// <summary>
    /// Computes a compact rank strictly greater than <paramref name="prev"/> (for appending at the end).
    /// </summary>
    /// <param name="prev">The current tail rank.</param>
    /// <returns>A rank strictly greater than <paramref name="prev"/>.</returns>
    public static string After(string prev)
    {
        ArgumentException.ThrowIfNullOrEmpty(prev);

        string intPart = ExtractIntegerPart(prev);
        string? incremented = IncrementInteger(intPart);
        return incremented is not null
            ? incremented
            : Midpoint(prev, null);
    }

    /// <summary>
    /// Computes a compact rank strictly less than <paramref name="next"/> (for prepending at the start).
    /// </summary>
    /// <param name="next">The current head rank.</param>
    /// <returns>A rank strictly less than <paramref name="next"/>.</returns>
    public static string Before(string next)
    {
        ArgumentException.ThrowIfNullOrEmpty(next);

        string intPart = ExtractIntegerPart(next);
        if (string.CompareOrdinal(intPart, next) < 0)
        {
            return intPart;
        }

        string? decremented = DecrementInteger(intPart);
        return decremented is not null
            ? decremented
            : Midpoint(string.Empty, next);
    }

    /// <summary>
    /// Generates <paramref name="count"/> evenly spaced, compact ranks in strictly ascending ordinal order
    /// (used for initial migration backfill and table rebalancing).
    /// </summary>
    /// <param name="count">The number of ranks to generate.</param>
    /// <returns>A list of <paramref name="count"/> strictly ascending rank strings.</returns>
    public static List<string> GenerateEvenlySpaced(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        List<string> ranks = new(count);
        if (count == 0)
        {
            return ranks;
        }

        string current = Initial;
        ranks.Add(current);

        for (int i = 1; i < count; i++)
        {
            current = After(current);
            ranks.Add(current);
        }

        return ranks;
    }

    private static string Midpoint(string a, string? b)
    {
        var sb = new StringBuilder(Math.Max(a.Length, b?.Length ?? 0) + 2);
        int i = 0;

        while (true)
        {
            int lo = i < a.Length ? DigitOf(a[i]) : 0;
            int hi = b is not null && i < b.Length ? DigitOf(b[i]) : Base;

            if (lo == hi)
            {
                sb.Append(Alphabet[lo]);
                i++;
                continue;
            }

            if (hi - lo > 1)
            {
                sb.Append(Alphabet[(lo + hi) / 2]);
                return sb.ToString();
            }

            // Adjacent digits (hi - lo == 1): emit lo, then push strictly above a[i+1..].
            sb.Append(Alphabet[lo]);
            int j = i + 1;

            while (j < a.Length)
            {
                int d = DigitOf(a[j]);
                if (d < Base - 1)
                {
                    sb.Append(Alphabet[(d + Base) / 2]);
                    return sb.ToString();
                }

                sb.Append(Alphabet[Base - 1]);
                j++;
            }

            sb.Append(MidChar);
            return sb.ToString();
        }
    }

    private static string ExtractIntegerPart(string rank)
    {
        char head = rank[0];
        int intDigits = GetIntegerDigitCount(head);
        int totalLen = 1 + intDigits;
        return rank.Length >= totalLen ? rank[..totalLen] : rank;
    }

    private static int GetIntegerDigitCount(char head)
        => head switch
        {
            >= 'a' and <= 'z' => head - 'a' + 1,
            >= 'A' and <= 'Z' => 'Z' - head + 1,
            _ => 1,
        };

    private static string? IncrementInteger(string intPart)
    {
        char head = intPart[0];
        if (head is not ((>= 'a' and <= 'z') or (>= 'A' and <= 'Z')))
        {
            return null;
        }

        char[] digits = intPart[1..].ToCharArray();
        bool carry = true;

        for (int i = digits.Length - 1; i >= 0 && carry; i--)
        {
            int next = DigitOf(digits[i]) + 1;
            if (next == Base)
            {
                digits[i] = Alphabet[0];
            }
            else
            {
                digits[i] = Alphabet[next];
                carry = false;
            }
        }

        if (!carry)
        {
            return string.Concat(head, new string(digits));
        }

        if (head == 'Z')
        {
            return Initial;
        }

        if (head == 'z')
        {
            return null;
        }

        char nextHead = (char)(head + 1);
        return nextHead >= 'a'
            ? string.Concat(nextHead, new string(Alphabet[0], digits.Length + 1))
            : string.Concat(nextHead, new string(Alphabet[0], digits.Length - 1));
    }

    private static string? DecrementInteger(string intPart)
    {
        char head = intPart[0];
        if (head is not ((>= 'a' and <= 'z') or (>= 'A' and <= 'Z')))
        {
            return null;
        }

        char[] digits = intPart[1..].ToCharArray();
        bool borrow = true;

        for (int i = digits.Length - 1; i >= 0 && borrow; i--)
        {
            int prev = DigitOf(digits[i]) - 1;
            if (prev < 0)
            {
                digits[i] = Alphabet[Base - 1];
            }
            else
            {
                digits[i] = Alphabet[prev];
                borrow = false;
            }
        }

        if (!borrow)
        {
            return string.Concat(head, new string(digits));
        }

        if (head == 'a')
        {
            return string.Concat('Z', Alphabet[Base - 1]);
        }

        if (head == 'A')
        {
            return null;
        }

        char prevHead = (char)(head - 1);
        return prevHead < 'a'
            ? string.Concat(prevHead, new string(Alphabet[Base - 1], digits.Length + 1))
            : string.Concat(prevHead, new string(Alphabet[Base - 1], digits.Length - 1));
    }

    private static int DigitOf(char c)
    {
        int index = c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'A' and <= 'Z' => c - 'A' + 10,
            >= 'a' and <= 'z' => c - 'a' + 36,
            _ => -1,
        };

        return index >= 0
            ? index
            : throw new ArgumentException($"Invalid fractional index character '{c}'.");
    }
}
