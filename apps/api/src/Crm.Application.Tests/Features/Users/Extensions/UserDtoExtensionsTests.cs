using Crm.Application.Dtos.Role;
using Crm.Application.Dtos.User;
using Crm.Application.Features.Users.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.Users.Extensions;

public class UserDtoExtensionsTests
{
    private readonly IFileUrlProvider _fileUrlProvider = Substitute.For<IFileUrlProvider>();

    [Fact]
    public void SecureAndEnrichWhenSelfShouldPreserveEmergencyContactAndUseSelfAvatar()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var baseUser = CreateTestUser(userId, "contact-123", "avatar.png", PronounCategory.Feminine);
        var roles = new[] { new RoleDto(Guid.NewGuid(), "Admin", "Adminka", "Admins") };

        _fileUrlProvider.ResolveUserAvatarUrl("avatar.png", userId, true)
            .Returns("https://api.crm.org/api/v1/users/me/avatar");

        // Act
        var result = baseUser.SecureAndEnrich(
            roles,
            _fileUrlProvider,
            currentUserId: userId,
            hasGlobalEmergencyContactAccess: false);

        // Assert
        result.EmergencyContact.ShouldBe("contact-123");
        result.AvatarUrl.ShouldBe("https://api.crm.org/api/v1/users/me/avatar");
        result.Roles.Single().Name.ShouldBe("Adminka");
    }

    [Fact]
    public void SecureAndEnrichWhenNotSelfAndNoAccessShouldMaskEmergencyContact()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var baseUser = CreateTestUser(userId, "contact-123", "avatar.png", PronounCategory.Masculine);
        var roles = new[] { new RoleDto(Guid.NewGuid(), "Admin", "Adminka", "Admins") };

        _fileUrlProvider.ResolveUserAvatarUrl("avatar.png", userId, false)
            .Returns($"https://api.crm.org/api/v1/users/{userId}/avatar");

        // Act
        var result = baseUser.SecureAndEnrich(
            roles,
            _fileUrlProvider,
            currentUserId: currentUserId,
            hasGlobalEmergencyContactAccess: false);

        // Assert
        result.EmergencyContact.ShouldBeNull();
        result.AvatarUrl.ShouldBe($"https://api.crm.org/api/v1/users/{userId}/avatar");
        result.Roles.Single().Name.ShouldBe("Admin");
    }

    [Fact]
    public void SecureAndEnrichWhenNotSelfAndHasGlobalAccessShouldPreserveEmergencyContact()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var baseUser = CreateTestUser(userId, "contact-123", null, null);
        var roles = new[] { new RoleDto(Guid.NewGuid(), "Admin", "Adminka", "Admins") };

        // Act
        var result = baseUser.SecureAndEnrich(
            roles,
            _fileUrlProvider,
            currentUserId: currentUserId,
            hasGlobalEmergencyContactAccess: true);

        // Assert
        result.EmergencyContact.ShouldBe("contact-123");
        result.AvatarUrl.ShouldBeNull();
        result.Roles.Single().Name.ShouldBe("Admin");
    }

    [Fact]
    public void SecureAndEnrichWhenProfileSelfShouldPreserveAllCustomFieldsAndEmergencyContact()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var customFields = new[]
        {
            new UserCustomFieldDto(Guid.NewGuid(), "PublicField", "val1", IsPublic: true),
            new UserCustomFieldDto(Guid.NewGuid(), "PrivateField", "val2", IsPublic: false),
        };
        var baseProfile = CreateTestUserProfile(userId, "contact-999", "avatar.png", PronounCategory.Feminine, customFields);
        var roles = new[] { new RoleDto(Guid.NewGuid(), "Admin", "Adminka", "Admins") };

        _fileUrlProvider.ResolveUserAvatarUrl("avatar.png", userId, true)
            .Returns("https://api.crm.org/api/v1/users/me/avatar");

        // Act
        var result = baseProfile.SecureAndEnrich(
            roles,
            _fileUrlProvider,
            currentUserId: userId,
            hasGlobalEmergencyContactAccess: false,
            hasGlobalCustomFieldsAccess: false);

        // Assert
        result.EmergencyContact.ShouldBe("contact-999");
        result.AvatarUrl.ShouldBe("https://api.crm.org/api/v1/users/me/avatar");
        result.Roles.Single().Name.ShouldBe("Adminka");
        result.CustomFields.Count().ShouldBe(2);
    }

    [Fact]
    public void SecureAndEnrichWhenProfileNotSelfAndNoAccessShouldMaskEmergencyContactAndFilterPrivateCustomFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var customFields = new[]
        {
            new UserCustomFieldDto(Guid.NewGuid(), "PublicField", "val1", IsPublic: true),
            new UserCustomFieldDto(Guid.NewGuid(), "PrivateField", "val2", IsPublic: false),
        };
        var baseProfile = CreateTestUserProfile(userId, "contact-999", "avatar.png", PronounCategory.Masculine, customFields);
        var roles = new[] { new RoleDto(Guid.NewGuid(), "Admin", "Adminka", "Admins") };

        _fileUrlProvider.ResolveUserAvatarUrl("avatar.png", userId, false)
            .Returns($"https://api.crm.org/api/v1/users/{userId}/avatar");

        // Act
        var result = baseProfile.SecureAndEnrich(
            roles,
            _fileUrlProvider,
            currentUserId: currentUserId,
            hasGlobalEmergencyContactAccess: false,
            hasGlobalCustomFieldsAccess: false);

        // Assert
        result.EmergencyContact.ShouldBeNull();
        result.AvatarUrl.ShouldBe($"https://api.crm.org/api/v1/users/{userId}/avatar");
        result.Roles.Single().Name.ShouldBe("Admin");
        result.CustomFields.ShouldHaveSingleItem().Key.ShouldBe("PublicField");
    }

    [Fact]
    public void SecureAndEnrichWhenProfileNotSelfAndHasGlobalAccessShouldIncludeAllCustomFieldsAndEmergencyContact()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var customFields = new[]
        {
            new UserCustomFieldDto(Guid.NewGuid(), "PublicField", "val1", IsPublic: true),
            new UserCustomFieldDto(Guid.NewGuid(), "PrivateField", "val2", IsPublic: false),
        };
        var baseProfile = CreateTestUserProfile(userId, "contact-999", null, null, customFields);
        var roles = new[] { new RoleDto(Guid.NewGuid(), "Admin", "Adminka", "Admins") };

        // Act
        var result = baseProfile.SecureAndEnrich(
            roles,
            _fileUrlProvider,
            currentUserId: currentUserId,
            hasGlobalEmergencyContactAccess: true,
            hasGlobalCustomFieldsAccess: true);

        // Assert
        result.EmergencyContact.ShouldBe("contact-999");
        result.AvatarUrl.ShouldBeNull();
        result.CustomFields.Count().ShouldBe(2);
    }

    [Fact]
    public void SecureAndEnrichWhenExplicitSelfFlagIsTrueShouldTreatAsSelf()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var customFields = new[]
        {
            new UserCustomFieldDto(Guid.NewGuid(), "PrivateField", "val2", IsPublic: false),
        };
        var baseProfile = CreateTestUserProfile(userId, "contact-999", null, null, customFields);
        var roles = new[] { new RoleDto(Guid.NewGuid(), "Admin", "Adminka", "Admins") };

        // Act (currentUserId != userId, but isExplicitSelf is true)
        var result = baseProfile.SecureAndEnrich(
            roles,
            _fileUrlProvider,
            currentUserId: Guid.NewGuid(),
            hasGlobalEmergencyContactAccess: false,
            hasGlobalCustomFieldsAccess: false,
            isExplicitSelf: true);

        // Assert
        result.EmergencyContact.ShouldBe("contact-999");
        result.CustomFields.ShouldHaveSingleItem().Key.ShouldBe("PrivateField");
    }

    private static UserDto CreateTestUser(Guid id, string? emergencyContact, string? avatarUrl, PronounCategory? pronounCategory)
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
            Roles: []);
    }

    private static UserProfileResult CreateTestUserProfile(
        Guid id,
        string? emergencyContact,
        string? avatarUrl,
        PronounCategory? pronounCategory,
        IEnumerable<UserCustomFieldDto> customFields)
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
            Roles: [],
            CustomFields: customFields);
    }
}
