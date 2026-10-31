using Crm.Domain.Enums;
using Crm.Domain.Extensions;
using Shouldly;

namespace Crm.Domain.Tests.Extensions;

/// <summary>
/// Contains unit tests for <see cref="FieldDataTypeExtensions"/>
/// verifying backfill JSON literal generation and unsupported type handling.
/// </summary>
public class FieldDataTypeExtensionsTests
{
    /// <summary>
    /// Verifies that all valid <see cref="FieldDataType"/> enum values return non-empty JSON literals.
    /// </summary>
    /// <param name="type">The field data type being tested.</param>
    /// <param name="expectedJson">The expected JSON string literal representation.</param>
    [Theory]
    [InlineData(FieldDataType.Text, "\"\"")]
    [InlineData(FieldDataType.Number, "0")]
    [InlineData(FieldDataType.Boolean, "false")]
    [InlineData(FieldDataType.Date, "\"1970-01-01\"")]
    [InlineData(FieldDataType.Email, "\"no-reply@example.com\"")]
    [InlineData(FieldDataType.Url, "\"https://example.com\"")]
    [InlineData(FieldDataType.PhoneNumber, "\"+100000000000\"")]
    public void GetBackfillJsonForStandardTypesShouldReturnExpectedLiteral(FieldDataType type, string expectedJson)
    {
        // Arrange & Act
        var result = type.GetBackfillJson();

        // Assert
        result.ShouldBe(expectedJson);
    }

    /// <summary>
    /// Verifies that <see cref="FieldDataType.DateTime"/> formats the Unix epoch using ISO 8601 round-trip format.
    /// </summary>
    [Fact]
    public void GetBackfillJsonForDateTimeShouldReturnIsoRoundTripLiteral()
    {
        // Arrange
        var expected = $"\"{DateTimeOffset.UnixEpoch:O}\"";

        // Act
        var result = FieldDataType.DateTime.GetBackfillJson();

        // Assert
        result.ShouldBe(expected);
    }

    /// <summary>
    /// Verifies that an unmapped or invalid <see cref="FieldDataType"/> enum value throws an <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void GetBackfillJsonForUndefinedTypeShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var undefinedType = (FieldDataType)999;

        // Act & Assert
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => undefinedType.GetBackfillJson());
        exception.ParamName.ShouldBe("type");
    }
}
