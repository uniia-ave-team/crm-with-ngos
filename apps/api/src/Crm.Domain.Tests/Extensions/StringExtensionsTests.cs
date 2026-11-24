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
    [InlineData("  Urgent  ", "URGENT")]
    [InlineData("\tТерміново\u00A0", "ТЕРМІНОВО")]
    [InlineData("розробка", "РОЗРОБКА")]
    public void NormalizeTitleShouldTrimUnicodeWhitespaceAndUppercaseInvariant(string title, string expected)
    {
        var result = title.NormalizeTitle();

        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(@"C:\fakepath\report.pdf", "report.pdf")]
    [InlineData("/tmp/uploads/report.pdf", "report.pdf")]
    [InlineData(@"C:\mixed/path\doc.txt", "doc.txt")]
    [InlineData("plain.csv", "plain.csv")]
    [InlineData(@"C:\fakepath\", "")]
    public void ExtractFileNameShouldStripBothWindowsAndUnixDirectorySeparators(string input, string expected)
    {
        var result = input.ExtractFileName();

        result.ShouldBe(expected);
    }
}
