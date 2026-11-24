using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Shouldly;

namespace Crm.Domain.Tests.Extensions;

/// <summary>
/// Unit tests for <see cref="SequentialOrderExtensions"/> verifying shared 0-based sequential ordering
/// validation, reordering, and compaction logic used by both field schemas and board topics.
/// </summary>
public class SequentialOrderExtensionsTests
{
    [Fact]
    public void EnsureSequentialOrdersShouldPassForContiguousZeroBasedItems()
    {
        Topic[] topics =
        [
            new() { Id = Guid.NewGuid(), Title = "Second", Order = 1 },
            new() { Id = Guid.NewGuid(), Title = "First", Order = 0 },
            new() { Id = Guid.NewGuid(), Title = "Third", Order = 2 },
        ];

        Should.NotThrow(() => topics.EnsureSequentialOrders());
    }

    [Fact]
    public void EnsureSequentialOrdersShouldThrowOnGapOrDuplicate()
    {
        Topic[] withGap =
        [
            new() { Id = Guid.NewGuid(), Title = "First", Order = 1 },
            new() { Id = Guid.NewGuid(), Title = "Second", Order = 2 },
        ];

        Should.Throw<NonSequentialOrderException>(() => withGap.EnsureSequentialOrders());

        Topic[] withDuplicate =
        [
            new() { Id = Guid.NewGuid(), Title = "First", Order = 0 },
            new() { Id = Guid.NewGuid(), Title = "Second", Order = 0 },
        ];

        Should.Throw<DomainValidationException>(() => withDuplicate.EnsureSequentialOrders());
    }

    [Fact]
    public void EnsureValidNextOrderShouldRejectNonZeroStartGapsAndDuplicates()
    {
        byte[] empty = [];
        Should.NotThrow(() => empty.EnsureValidNextOrder(0, () => new EntityAlreadyExistsException("Topic", 0)));
        Should.Throw<NonSequentialOrderException>(() => empty.EnsureValidNextOrder(1, () => new EntityAlreadyExistsException("Topic", 1)));
        Should.Throw<NonSequentialOrderException>(() => empty.EnsureValidNextOrder(3, () => new EntityAlreadyExistsException("Topic", 3)));

        byte[] existing = [0, 1];
        Should.NotThrow(() => existing.EnsureValidNextOrder(2, () => new EntityAlreadyExistsException("Topic", 2)));
        Should.Throw<EntityAlreadyExistsException>(() => existing.EnsureValidNextOrder(0, () => new EntityAlreadyExistsException("Topic", 0)));
        Should.Throw<EntityAlreadyExistsException>(() => existing.EnsureValidNextOrder(1, () => new EntityAlreadyExistsException("Topic", 1)));
        Should.Throw<NonSequentialOrderException>(() => existing.EnsureValidNextOrder(3, () => new EntityAlreadyExistsException("Topic", 3)));
    }

    [Fact]
    public void ReorderSequentialShouldShiftNeighborsAndRejectOutOfBoundsOrder()
    {
        var t0 = new Topic { Id = Guid.NewGuid(), Title = "A", Order = 0 };
        var t1 = new Topic { Id = Guid.NewGuid(), Title = "B", Order = 1 };
        var t2 = new Topic { Id = Guid.NewGuid(), Title = "C", Order = 2 };
        List<Topic> topics = [t0, t1, t2];

        // Out of bounds (>= count) is rejected
        Should.Throw<NonSequentialOrderException>(() =>
            topics.ReorderSequential(t0, 3, static (t, o) => t.Order = o));

        // Move C (2) -> 0 shifts A (0->1) and B (1->2)
        topics.ReorderSequential(t2, 0, static (t, o) => t.Order = o);
        t2.Order.ShouldBe((byte)0);
        t0.Order.ShouldBe((byte)1);
        t1.Order.ShouldBe((byte)2);

        // Move C (0) -> 2 shifts A (1->0) and B (2->1)
        topics.ReorderSequential(t2, 2, static (t, o) => t.Order = o);
        t0.Order.ShouldBe((byte)0);
        t1.Order.ShouldBe((byte)1);
        t2.Order.ShouldBe((byte)2);
    }

    [Fact]
    public void CompactSequentialShouldCloseGapsAfterDeletion()
    {
        var t1 = new Topic { Id = Guid.NewGuid(), Title = "B", Order = 1 };
        var t3 = new Topic { Id = Guid.NewGuid(), Title = "D", Order = 3 };
        List<Topic> remaining = [t3, t1];

        remaining.CompactSequential(static (t, o) => t.Order = o);

        t1.Order.ShouldBe((byte)0);
        t3.Order.ShouldBe((byte)1);
    }
}
