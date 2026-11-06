using Crm.Domain.Extensions;
using Shouldly;

namespace Crm.Domain.Tests.Extensions;

public class StringExtensionsTests
{
    [Theory]
    [InlineData("FullName", "FullName")]
    [InlineData("fullname", "FullName")]
    [InlineData("FULLNAME", "FullName")]
    [InlineData("LastName", "LastName")]
    [InlineData("lastname", "LastName")]
    [InlineData("FirstName", "FirstName")]
    [InlineData("firstname", "FirstName")]
    public void EqualsAnyIgnoreCaseWhenSourceMatchesTargetShouldReturnTrue(string source, string target)
    {
        // Act
        var result = source.EqualsAnyIgnoreCase(target);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void EqualsAnyIgnoreCaseWhenSourceMatchesOneOfMultipleTargetsShouldReturnTrue()
    {
        // Act
        var result = "LastName".EqualsAnyIgnoreCase("FullName", "LastName", "FirstName");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void EqualsAnyIgnoreCaseWhenNoTargetsMatchShouldReturnFalse()
    {
        // Act
        var result = "Email".EqualsAnyIgnoreCase("FullName", "LastName");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void EqualsAnyIgnoreCaseWhenSourceIsNullShouldReturnFalse()
    {
        // Arrange
        string? nullString = null;

        // Act
        var result = nullString.EqualsAnyIgnoreCase("FullName", "LastName");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void EqualsAnyIgnoreCaseWhenTargetsEmptyShouldReturnFalse()
    {
        // Act
        var result = "FullName".EqualsAnyIgnoreCase();

        // Assert
        result.ShouldBeFalse();
    }

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
        var result = term!.ToContainsLikePattern();

        // Assert
        result.ShouldBe(string.Empty);
    }
}
