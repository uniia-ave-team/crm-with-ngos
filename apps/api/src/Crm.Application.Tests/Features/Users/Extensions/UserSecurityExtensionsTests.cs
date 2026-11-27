using System.Text.Json.Nodes;
using Crm.Application.Features.Users.Extensions;
using Shouldly;

namespace Crm.Application.Tests.Features.Users.Extensions;

public class UserSecurityExtensionsTests
{
    private static readonly string[] AllKeys = ["public_field", "private_field"];
    private static readonly string[] PublicKeys = ["public_field"];

    [Fact]
    public void ProtectEmergencyContactWhenIsSelfShouldReturnContact()
    {
        // Act
        var result = UserSecurityExtensions.ProtectEmergencyContact("Contact info", isSelf: true, hasGlobalAccess: false);

        // Assert
        result.ShouldBe("Contact info");
    }

    [Fact]
    public void ProtectEmergencyContactWhenNotSelfWithGlobalAccessShouldReturnContact()
    {
        // Act
        var result = UserSecurityExtensions.ProtectEmergencyContact("Contact info", isSelf: false, hasGlobalAccess: true);

        // Assert
        result.ShouldBe("Contact info");
    }

    [Fact]
    public void ProtectEmergencyContactWhenNotSelfAndNoGlobalAccessShouldReturnNull()
    {
        // Act
        var result = UserSecurityExtensions.ProtectEmergencyContact("Contact info", isSelf: false, hasGlobalAccess: false);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ProtectEmergencyContactWhenContactIsNullOrWhitespaceShouldReturnContactOrNull()
    {
        // Act
        var result = UserSecurityExtensions.ProtectEmergencyContact(null, isSelf: true, hasGlobalAccess: true);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ProtectCustomFieldsWhenIsSelfShouldReturnAllOrderedCustomFields()
    {
        // Arrange
        var payload = new JsonObject
        {
            ["private_field"] = "val2",
            ["public_field"] = "val1",
        };

        // Act
        var result = payload.ProtectCustomFields(AllKeys, PublicKeys, isSelf: true, hasGlobalAccess: false);

        // Assert
        result.Select(kvp => kvp.Key).ShouldBe(["public_field", "private_field"]);
        result["public_field"]?.GetValue<string>().ShouldBe("val1");
        result["private_field"]?.GetValue<string>().ShouldBe("val2");
    }

    [Fact]
    public void ProtectCustomFieldsWhenNotSelfWithGlobalAccessShouldReturnAllOrderedCustomFields()
    {
        // Arrange
        var payload = new JsonObject
        {
            ["public_field"] = "val1",
            ["private_field"] = "val2",
        };

        // Act
        var result = payload.ProtectCustomFields(AllKeys, PublicKeys, isSelf: false, hasGlobalAccess: true);

        // Assert
        result.Count.ShouldBe(2);
    }

    [Fact]
    public void ProtectCustomFieldsWhenNotSelfAndNoGlobalAccessShouldReturnOnlyPublicFields()
    {
        // Arrange
        var payload = new JsonObject
        {
            ["public_field"] = "val1",
            ["private_field"] = "val2",
        };

        // Act
        var result = payload.ProtectCustomFields(AllKeys, PublicKeys, isSelf: false, hasGlobalAccess: false);

        // Assert
        result.Select(kvp => kvp.Key).ShouldBe(["public_field"]);
        result["public_field"]?.GetValue<string>().ShouldBe("val1");
    }
}
