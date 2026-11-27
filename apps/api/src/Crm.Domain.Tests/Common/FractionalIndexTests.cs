using Crm.Domain.Common;
using Shouldly;

namespace Crm.Domain.Tests.Common;

/// <summary>
/// Unit tests verifying lexicographical base-62 <see cref="FractionalIndex"/> rank generation,
/// strict ASCII ordinal ordering (matching PostgreSQL <c>COLLATE "C"</c>), and boundary transitions.
/// </summary>
public class FractionalIndexTests
{
    [Fact]
    public void BetweenWhenBothBoundsAreNullShouldReturnInitialRank()
    {
        FractionalIndex.Between(null, null).ShouldBe(FractionalIndex.Initial);
        FractionalIndex.Between(string.Empty, string.Empty).ShouldBe(FractionalIndex.Initial);
    }

    [Fact]
    public void AfterShouldGrowCompactlyAcrossBase62CarriesAndPreserveOrdinalOrder()
    {
        string current = FractionalIndex.Initial;
        var seen = new HashSet<string>(StringComparer.Ordinal) { current };

        for (int i = 0; i < 4000; i++)
        {
            string next = FractionalIndex.After(current);
            string.CompareOrdinal(current, next).ShouldBeLessThan(0);
            seen.Add(next).ShouldBeTrue();
            current = next;
        }

        // Across 4000 sequential appends, rank length stays <= 4 characters (a0 -> az -> b00 -> bzz -> c000).
        current.Length.ShouldBeLessThanOrEqualTo(4);
    }

    [Fact]
    public void BeforeShouldShrinkCompactlyAcrossZeroAndNegativeBucketsAndPreserveOrdinalOrder()
    {
        string current = FractionalIndex.Initial;
        var seen = new HashSet<string>(StringComparer.Ordinal) { current };

        for (int i = 0; i < 4000; i++)
        {
            string prev = FractionalIndex.Before(current);
            string.CompareOrdinal(prev, current).ShouldBeLessThan(0);
            seen.Add(prev).ShouldBeTrue();
            current = prev;
        }

        // a0 -> Zz -> Z0 -> Yzz -> Y00 -> Xzzz stays compact (<= 4 characters).
        current.Length.ShouldBeLessThanOrEqualTo(4);
    }

    [Fact]
    public void BetweenShouldHandleBucketBoundariesAndMigrationBackfillRanks()
    {
        // Bucket boundary between Zz and a0
        string zToA = FractionalIndex.Between("Zz", "a0");
        string.CompareOrdinal("Zz", zToA).ShouldBeLessThan(0);
        string.CompareOrdinal(zToA, "a0").ShouldBeLessThan(0);

        string afterZToA = FractionalIndex.Between(zToA, "a0");
        string.CompareOrdinal(zToA, afterZToA).ShouldBeLessThan(0);
        string.CompareOrdinal(afterZToA, "a0").ShouldBeLessThan(0);

        // Migration backfill format boundary (e0000z -> e00010)
        string midBackfill = FractionalIndex.Between("e0000z", "e00010");
        string.CompareOrdinal("e0000z", midBackfill).ShouldBeLessThan(0);
        string.CompareOrdinal(midBackfill, "e00010").ShouldBeLessThan(0);

        // Fractional subdivision between adjacent ranks
        string mid = FractionalIndex.Between("a0", "a1");
        string.CompareOrdinal("a0", mid).ShouldBeLessThan(0);
        string.CompareOrdinal(mid, "a1").ShouldBeLessThan(0);
    }

    [Fact]
    public void BetweenShouldPreserveStrictOrderAcross250RepeatedMiddleInserts()
    {
        string left = "a0";
        string right = "a1";
        List<string> inserted = [];

        for (int i = 0; i < 250; i++)
        {
            string mid = FractionalIndex.Between(left, right);
            string.CompareOrdinal(left, mid).ShouldBeLessThan(0);
            string.CompareOrdinal(mid, right).ShouldBeLessThan(0);
            inserted.Add(mid);

            // Alternate pushing against left and right bounds to exercise both midpoint branches.
            if (i % 2 == 0)
            {
                right = mid;
            }
            else
            {
                left = mid;
            }
        }

        inserted.Distinct(StringComparer.Ordinal).Count().ShouldBe(250);
    }

    [Fact]
    public void BetweenWhenPrevIsGreaterThanOrEqualToNextShouldThrowArgumentException()
    {
        Should.Throw<ArgumentException>(() => FractionalIndex.Between("a1", "a1"));
        Should.Throw<ArgumentException>(() => FractionalIndex.Between("a2", "a1"));
    }

    [Fact]
    public void GenerateEvenlySpacedShouldReturnStrictlyAscendingCompactRanks()
    {
        FractionalIndex.GenerateEvenlySpaced(0).ShouldBeEmpty();
        FractionalIndex.GenerateEvenlySpaced(1).ShouldBe([FractionalIndex.Initial]);

        var ranks = FractionalIndex.GenerateEvenlySpaced(500);
        ranks.Count.ShouldBe(500);

        for (int i = 1; i < ranks.Count; i++)
        {
            string.CompareOrdinal(ranks[i - 1], ranks[i]).ShouldBeLessThan(0);
        }
    }
}
