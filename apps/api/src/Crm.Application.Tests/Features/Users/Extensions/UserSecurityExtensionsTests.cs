using Crm.Application.Dtos.User;
using Crm.Application.Features.Users.Extensions;
using Shouldly;

namespace Crm.Application.Tests.Features.Users.Extensions;

public class UserSecurityExtensionsTests
{
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
    public void ProtectCustomFieldsWhenIsSelfShouldReturnAllCustomFields()
    {
        // Arrange
        var fields = new List<UserCustomFieldDto>
        {
            new(Guid.NewGuid(), "PublicField", "val1", IsPublic: true),
            new(Guid.NewGuid(), "PrivateField", "val2", IsPublic: false),
        };

        // Act
        var result = fields.ProtectCustomFields(isSelf: true, hasGlobalAccess: false);

        // Assert
        result.Count().ShouldBe(2);
    }

    [Fact]
    public void ProtectCustomFieldsWhenNotSelfWithGlobalAccessShouldReturnAllCustomFields()
    {
        // Arrange
        var fields = new List<UserCustomFieldDto>
        {
            new(Guid.NewGuid(), "PublicField", "val1", IsPublic: true),
            new(Guid.NewGuid(), "PrivateField", "val2", IsPublic: false),
        };

        // Act
        var result = fields.ProtectCustomFields(isSelf: false, hasGlobalAccess: true);

        // Assert
        result.Count().ShouldBe(2);
    }

    [Fact]
    public void ProtectCustomFieldsWhenNotSelfAndNoGlobalAccessShouldReturnOnlyPublicFields()
    {
        // Arrange
        var publicField = new UserCustomFieldDto(Guid.NewGuid(), "PublicField", "val1", IsPublic: true);
        var privateField = new UserCustomFieldDto(Guid.NewGuid(), "PrivateField", "val2", IsPublic: false);
        var fields = new List<UserCustomFieldDto> { publicField, privateField };

        // Act
        var result = fields.ProtectCustomFields(isSelf: false, hasGlobalAccess: false).ToList();

        // Assert
        result.ShouldHaveSingleItem().Key.ShouldBe("PublicField");
    }
}
