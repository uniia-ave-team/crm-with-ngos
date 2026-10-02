using Crm.Domain.Enums;
using Crm.Domain.Extensions;
using Shouldly;

namespace Crm.Domain.Tests.Extensions;

/// <summary>
/// Contains unit tests for the <see cref="PermissionExtensions"/> utility class
/// to ensure correct transformation of AccessRight enums into claim strings.
/// </summary>
public class PermissionExtensionsTests
{
    /// <summary>
    /// Verifies that an individual <see cref="AccessRight"/> enum value is correctly
    /// transformed into its string claim representation with the proper prefix.
    /// </summary>
    [Fact]
    public void ToClaimValueShouldReturnCorrectFormattedString()
    {
        // Arrange
        var permission = AccessRight.CreateUser;
        var expectedClaimString = "Permissions.CreateUser";

        // Act
        var claimValue = permission.ToClaimValue();

        // Assert
        claimValue.ShouldBe(expectedClaimString);
    }

    /// <summary>
    /// Verifies that the method retrieving all permission string values returns
    /// a complete collection representing all <see cref="AccessRight"/> enum values.
    /// </summary>
    [Fact]
    public void GetAllStringValuesShouldReturnAllEnumValuesFormatted()
    {
        // Arrange
        var expectedEnumValuesCount = Enum.GetValues<AccessRight>().Length;

        // Act
        var allPermissions = PermissionExtensions.GetAllStringValues();

        // Assert
        allPermissions.Count.ShouldBe(expectedEnumValuesCount);
        allPermissions.ShouldContain("Permissions.ViewUser");
        allPermissions.ShouldContain("Permissions.ManageRole");
    }
}
