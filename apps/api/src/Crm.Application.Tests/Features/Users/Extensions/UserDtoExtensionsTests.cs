using System.Text.Json.Nodes;
using Crm.Application.Dtos.Roles;
using Crm.Application.Dtos.Users;
using Crm.Application.Features.Users.Extensions;
using Crm.Application.Features.Users.Mappings;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Mapster;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.Users.Extensions;

public class UserDtoExtensionsTests
{
    private static readonly string[] AllKeys = ["public_field", "private_field"];
    private static readonly string[] PublicKeys = ["public_field"];

    private readonly IFileUrlProvider _fileUrlProvider = Substitute.For<IFileUrlProvider>();

    static UserDtoExtensionsTests()
    {
        new UserMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
    }

    [Fact]
    public void SecureAndEnrichWhenSelfShouldPreserveEmergencyContactAllCustomFieldsAndUseSelfAvatar()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var baseUser = CreateTestUser(
            userId,
            "contact-123",
            "avatar.png",
            PronounCategory.Feminine,
            new JsonObject { ["private_field"] = "42", ["public_field"] = "val1" });
        var roles = new[] { new RoleDto(Guid.NewGuid(), "Admin", "Adminka", "Admins") };

        _fileUrlProvider.ResolveUserAvatarUrl("avatar.png", userId, true)
            .Returns("https://api.crm.org/api/v1/users/me/avatar");

        // Act
        var result = baseUser.SecureAndEnrich(
            roles,
            AllKeys,
            PublicKeys,
            _fileUrlProvider,
            currentUserId: userId,
            hasGlobalEmergencyContactAccess: false,
            hasGlobalCustomFieldsAccess: false);

        // Assert
        result.EmergencyContact.ShouldBe("contact-123");
        result.AvatarUrl.ShouldBe("https://api.crm.org/api/v1/users/me/avatar");
        result.IsActive.ShouldBeTrue();
        result.Roles.Single().Name.ShouldBe("Adminka");
        result.CustomFields.Select(kvp => kvp.Key).ShouldBe(["public_field", "private_field"]);
        result.CustomFields["public_field"]?.GetValue<string>().ShouldBe("val1");
        result.CustomFields["private_field"]?.GetValue<string>().ShouldBe("42");
    }

    [Fact]
    public void SecureAndEnrichWhenInactiveUserDtoShouldPreserveIsActiveFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var baseUser = CreateTestUser(userId, null, null, null, [], isActive: false);
        var roles = Array.Empty<RoleDto>();

        // Act
        var result = baseUser.SecureAndEnrich(
            roles,
            [],
            [],
            _fileUrlProvider,
            currentUserId: Guid.NewGuid(),
            hasGlobalEmergencyContactAccess: false,
            hasGlobalCustomFieldsAccess: false);

        // Assert
        result.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void SecureAndEnrichWhenNotSelfAndNoAccessShouldMaskEmergencyContactAndFilterPrivateCustomFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var baseUser = CreateTestUser(
            userId,
            "contact-123",
            "avatar.png",
            PronounCategory.Masculine,
            new JsonObject { ["public_field"] = "val1", ["private_field"] = "secret" });
        var roles = new[] { new RoleDto(Guid.NewGuid(), "Admin", "Adminka", "Admins") };

        _fileUrlProvider.ResolveUserAvatarUrl("avatar.png", userId, false)
            .Returns($"https://api.crm.org/api/v1/users/{userId}/avatar");

        // Act
        var result = baseUser.SecureAndEnrich(
            roles,
            AllKeys,
            PublicKeys,
            _fileUrlProvider,
            currentUserId: currentUserId,
            hasGlobalEmergencyContactAccess: false,
            hasGlobalCustomFieldsAccess: false);

        // Assert
        result.EmergencyContact.ShouldBeNull();
        result.AvatarUrl.ShouldBe($"https://api.crm.org/api/v1/users/{userId}/avatar");
        result.Roles.Single().Name.ShouldBe("Admin");
        result.CustomFields.Select(kvp => kvp.Key).ShouldBe(["public_field"]);
    }

    [Fact]
    public void SecureAndEnrichWhenNotSelfAndHasGlobalAccessShouldPreserveEmergencyContactAndAllCustomFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var baseUser = CreateTestUser(
            userId,
            "contact-123",
            null,
            null,
            new JsonObject { ["public_field"] = "val1", ["private_field"] = "secret" });
        var roles = new[] { new RoleDto(Guid.NewGuid(), "Admin", "Adminka", "Admins") };

        // Act
        var result = baseUser.SecureAndEnrich(
            roles,
            AllKeys,
            PublicKeys,
            _fileUrlProvider,
            currentUserId: currentUserId,
            hasGlobalEmergencyContactAccess: true,
            hasGlobalCustomFieldsAccess: true);

        // Assert
        result.EmergencyContact.ShouldBe("contact-123");
        result.AvatarUrl.ShouldBeNull();
        result.Roles.Single().Name.ShouldBe("Admin");
        result.CustomFields.Count.ShouldBe(2);
    }

    [Fact]
    public void SecureAndEnrichWhenProfileSelfShouldPreserveAllCustomFieldsAndEmergencyContact()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var baseProfile = CreateTestUserProfile(
            userId,
            "contact-999",
            "avatar.png",
            PronounCategory.Feminine,
            new JsonObject { ["public_field"] = "val1", ["private_field"] = "val2" });
        var roles = new[] { new RoleDto(Guid.NewGuid(), "Admin", "Adminka", "Admins") };

        _fileUrlProvider.ResolveUserAvatarUrl("avatar.png", userId, true)
            .Returns("https://api.crm.org/api/v1/users/me/avatar");

        // Act
        var result = baseProfile.SecureAndEnrich(
            roles,
            AllKeys,
            PublicKeys,
            _fileUrlProvider,
            currentUserId: userId,
            hasGlobalEmergencyContactAccess: false,
            hasGlobalCustomFieldsAccess: false);

        // Assert
        result.EmergencyContact.ShouldBe("contact-999");
        result.PronounCategory.ShouldBe(PronounCategory.Feminine);
        result.AvatarUrl.ShouldBe("https://api.crm.org/api/v1/users/me/avatar");
        result.IsActive.ShouldBeTrue();
        result.Roles.Single().Name.ShouldBe("Adminka");
        result.CustomFields.Count.ShouldBe(2);
    }

    [Fact]
    public void SecureAndEnrichWhenInactiveUserProfileShouldPreserveIsActiveFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var baseProfile = CreateTestUserProfile(userId, null, null, null, [], isActive: false);
        var roles = Array.Empty<RoleDto>();

        // Act
        var result = baseProfile.SecureAndEnrich(
            roles,
            [],
            [],
            _fileUrlProvider,
            currentUserId: Guid.NewGuid(),
            hasGlobalEmergencyContactAccess: false,
            hasGlobalCustomFieldsAccess: false);

        // Assert
        result.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void SecureAndEnrichWhenProfileNotSelfAndNoAccessShouldMaskEmergencyContactAndFilterPrivateCustomFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var baseProfile = CreateTestUserProfile(
            userId,
            "contact-999",
            "avatar.png",
            PronounCategory.Masculine,
            new JsonObject { ["public_field"] = "val1", ["private_field"] = "val2" });
        var roles = new[] { new RoleDto(Guid.NewGuid(), "Admin", "Adminka", "Admins") };

        _fileUrlProvider.ResolveUserAvatarUrl("avatar.png", userId, false)
            .Returns($"https://api.crm.org/api/v1/users/{userId}/avatar");

        // Act
        var result = baseProfile.SecureAndEnrich(
            roles,
            AllKeys,
            PublicKeys,
            _fileUrlProvider,
            currentUserId: currentUserId,
            hasGlobalEmergencyContactAccess: false,
            hasGlobalCustomFieldsAccess: false);

        // Assert
        result.EmergencyContact.ShouldBeNull();
        result.PronounCategory.ShouldBe(PronounCategory.Masculine);
        result.AvatarUrl.ShouldBe($"https://api.crm.org/api/v1/users/{userId}/avatar");
        result.Roles.Single().Name.ShouldBe("Admin");
        result.CustomFields.Select(kvp => kvp.Key).ShouldBe(["public_field"]);
    }

    [Fact]
    public void SecureAndEnrichWhenProfileNotSelfAndHasGlobalAccessShouldIncludeAllCustomFieldsAndEmergencyContact()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var baseProfile = CreateTestUserProfile(
            userId,
            "contact-999",
            null,
            null,
            new JsonObject { ["public_field"] = "val1", ["private_field"] = "val2" });
        var roles = new[] { new RoleDto(Guid.NewGuid(), "Admin", "Adminka", "Admins") };

        // Act
        var result = baseProfile.SecureAndEnrich(
            roles,
            AllKeys,
            PublicKeys,
            _fileUrlProvider,
            currentUserId: currentUserId,
            hasGlobalEmergencyContactAccess: true,
            hasGlobalCustomFieldsAccess: true);

        // Assert
        result.EmergencyContact.ShouldBe("contact-999");
        result.PronounCategory.ShouldBeNull();
        result.AvatarUrl.ShouldBeNull();
        result.CustomFields.Count.ShouldBe(2);
    }

    [Fact]
    public void SecureAndEnrichWhenExplicitSelfFlagIsTrueShouldTreatAsSelf()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var baseProfile = CreateTestUserProfile(
            userId,
            "contact-999",
            null,
            null,
            new JsonObject { ["private_field"] = "val2" });
        var roles = new[] { new RoleDto(Guid.NewGuid(), "Admin", "Adminka", "Admins") };

        // Act (currentUserId != userId, but isExplicitSelf is true)
        var result = baseProfile.SecureAndEnrich(
            roles,
            ["private_field"],
            [],
            _fileUrlProvider,
            currentUserId: Guid.NewGuid(),
            hasGlobalEmergencyContactAccess: false,
            hasGlobalCustomFieldsAccess: false,
            isExplicitSelf: true);

        // Assert
        result.EmergencyContact.ShouldBe("contact-999");
        result.CustomFields.Select(kvp => kvp.Key).ShouldBe(["private_field"]);
    }

    private static UserDto CreateTestUser(
        Guid id,
        string? emergencyContact,
        string? avatarUrl,
        PronounCategory? pronounCategory,
        JsonObject? customFields = null,
        bool isActive = true)
    {
        return new UserDto(
            Id: id,
            Email: "test@example.com",
            FirstName: "Test",
            LastName: "User",
            Patronymic: "Mid",
            PhoneNumber: "+123456789",
            Country: "Ukraine",
            EmergencyContact: emergencyContact,
            Pronouns: "they/them",
            PronounCategory: pronounCategory,
            AvatarUrl: avatarUrl,
            IsActive: isActive,
            Roles: [],
            CustomFields: customFields ?? []);
    }

    private static UserProfileResult CreateTestUserProfile(
        Guid id,
        string? emergencyContact,
        string? avatarUrl,
        PronounCategory? pronounCategory,
        JsonObject customFields,
        bool isActive = true)
    {
        return new UserProfileResult(
            Id: id,
            Email: "profile@example.com",
            FirstName: "ProfileFirst",
            LastName: "ProfileLast",
            Patronymic: "ProfilePatronymic",
            PhoneNumber: "+380501112233",
            Country: "Ukraine",
            EmergencyContact: emergencyContact,
            PreferredLanguage: "uk",
            Pronouns: "she/her",
            PronounCategory: pronounCategory,
            AvatarUrl: avatarUrl,
            IsActive: isActive,
            Roles: [],
            CustomFields: customFields);
    }
}
