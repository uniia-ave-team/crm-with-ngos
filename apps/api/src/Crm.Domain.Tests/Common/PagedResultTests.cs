using Crm.Domain.Common;
using Shouldly;

namespace Crm.Domain.Tests.Common;

/// <summary>
/// Contains unit tests for the <see cref="PagedResult{T}"/> class
/// to ensure pagination logic and calculations are correct.
/// </summary>
public class PagedResultTests
{
    /// <summary>
    /// Verifies that the pagination properties (TotalPages, HasPreviousPage, HasNextPage)
    /// are calculated correctly based on different combinations of total count and page size.
    /// </summary>
    /// <param name="totalCount">The total number of items available.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="pageNumber">The current page number.</param>
    /// <param name="expectedTotalPages">The expected calculated total pages.</param>
    /// <param name="expectedHasPrevious">The expected boolean value for having a previous page.</param>
    /// <param name="expectedHasNext">The expected boolean value for having a next page.</param>
    [Theory]
    [InlineData(0, 10, 1, 0, false, false)]
    [InlineData(5, 10, 1, 1, false, false)]
    [InlineData(15, 10, 1, 2, false, true)]
    [InlineData(15, 10, 2, 2, true, false)]
    [InlineData(20, 10, 1, 2, false, true)]
    [InlineData(20, 10, 2, 2, true, false)]
    public void PagedResultShouldCalculatePaginationCorrectly(
        int totalCount,
        int pageSize,
        int pageNumber,
        int expectedTotalPages,
        bool expectedHasPrevious,
        bool expectedHasNext)
    {
        // Arrange
        var emptyItems = Array.Empty<string>();

        // Act
        var result = new PagedResult<string>(emptyItems, totalCount, pageNumber, pageSize);

        // Assert
        result.TotalPages.ShouldBe(expectedTotalPages);
        result.HasPreviousPage.ShouldBe(expectedHasPrevious);
        result.HasNextPage.ShouldBe(expectedHasNext);
    }
}
