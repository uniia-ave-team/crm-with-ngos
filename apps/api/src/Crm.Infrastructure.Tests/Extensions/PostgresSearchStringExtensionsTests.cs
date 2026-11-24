using Crm.Infrastructure.Persistence.Extensions;
using Shouldly;

namespace Crm.Infrastructure.Tests.Extensions;

/// <summary>
/// Verifies PostgreSQL-specific <c>ILIKE</c> escaping and <c>to_tsquery</c> prefix query building helpers.
/// </summary>
public class PostgresSearchStringExtensionsTests
{
    [Theory]
    [InlineData("john", "%john%")]
    [InlineData("100%", "%100\\%%")]
    [InlineData("first_name", "%first\\_name%")]
    [InlineData("a\\b", "%a\\\\b%")]
    public void ToContainsLikePatternWhenValidTermShouldEscapeAndWrap(string term, string expected)
    {
        // Act
        var result = term.ToContainsLikePattern();

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToContainsLikePatternWhenNullOrWhitespaceShouldReturnEmpty(string? term)
    {
        // Act
        var result = term.ToContainsLikePattern();

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("Kyi", "kyi:*")]
    [InlineData("  Kyiv   Donor ", "kyiv:* & donor:*")]
    [InlineData("2026-10", "2026:* & 10:*")]
    [InlineData("150.50", "150:* & 50:*")]
    [InlineData("non-profit", "non:* & profit:*")]
    [InlineData("foo & bar | !baz", "foo:* & bar:* & baz:*")]
    public void BuildPrefixTsQueryWithValidSearchTermsShouldFormatPrefixQuery(string searchTerm, string expectedQuery)
    {
        // Act
        var result = searchTerm.BuildPrefixTsQuery();

        // Assert
        result.ShouldBe(expectedQuery);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("&&& !!! ::: ***")]
    public void BuildPrefixTsQueryWithEmptyOrOperatorOnlyInputShouldReturnNull(string? searchTerm)
    {
        // Act
        var result = searchTerm.BuildPrefixTsQuery();

        // Assert
        result.ShouldBeNull();
    }
}
