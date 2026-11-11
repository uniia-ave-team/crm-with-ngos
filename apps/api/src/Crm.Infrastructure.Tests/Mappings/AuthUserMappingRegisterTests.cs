using Crm.Application.Dtos.User;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Mappings;
using Crm.Infrastructure.Persistence;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Crm.Infrastructure.Tests.Mappings;

/// <summary>
/// Contains unit tests verifying Mapster mapping and projection registrations in <see cref="AuthUserMappingRegister"/>.
/// </summary>
public class AuthUserMappingRegisterTests
{
    private readonly TypeAdapterConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthUserMappingRegisterTests"/> class.
    /// Sets up an isolated <see cref="TypeAdapterConfig"/> instance with <see cref="AuthUserMappingRegister"/>.
    /// </summary>
    public AuthUserMappingRegisterTests()
    {
        _config = new TypeAdapterConfig();
        new AuthUserMappingRegister().Register(_config);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProjectToUserDtoShouldMapIsActiveAndProfilePropertiesFromUserProfile(bool isActive)
    {
        // Arrange
        var authUser = CreateAuthUser(isActive);

        // Act
        var projected = new[] { authUser }
            .AsQueryable()
            .ProjectToType<UserDto>(_config)
            .Single();

        // Assert
        projected.Id.ShouldBe(authUser.Id);
        projected.Email.ShouldBe(authUser.Email);
        projected.FirstName.ShouldBe(authUser.UserProfile.FirstName);
        projected.LastName.ShouldBe(authUser.UserProfile.LastName);
        projected.Patronymic.ShouldBe(authUser.UserProfile.Patronymic);
        projected.PhoneNumber.ShouldBe(authUser.UserProfile.PhoneNumber);
        projected.Country.ShouldBe(authUser.UserProfile.Country);
        projected.EmergencyContact.ShouldBe(authUser.UserProfile.EmergencyContact);
        projected.Pronouns.ShouldBe(authUser.UserProfile.Pronouns);
        projected.PronounCategory.ShouldBe(authUser.UserProfile.PronounCategory);
        projected.AvatarUrl.ShouldBe(authUser.UserProfile.Avatar);
        projected.IsActive.ShouldBe(isActive);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProjectToUserProfileResultShouldMapIsActiveAndProfilePropertiesFromUserProfile(bool isActive)
    {
        // Arrange
        var authUser = CreateAuthUser(isActive);

        // Act
        var projected = new[] { authUser }
            .AsQueryable()
            .ProjectToType<UserProfileResult>(_config)
            .Single();

        // Assert
        projected.Id.ShouldBe(authUser.Id);
        projected.Email.ShouldBe(authUser.Email);
        projected.FirstName.ShouldBe(authUser.UserProfile.FirstName);
        projected.LastName.ShouldBe(authUser.UserProfile.LastName);
        projected.Patronymic.ShouldBe(authUser.UserProfile.Patronymic);
        projected.PhoneNumber.ShouldBe(authUser.UserProfile.PhoneNumber);
        projected.Country.ShouldBe(authUser.UserProfile.Country);
        projected.EmergencyContact.ShouldBe(authUser.UserProfile.EmergencyContact);
        projected.PreferredLanguage.ShouldBe(authUser.UserProfile.PreferredLanguage);
        projected.Pronouns.ShouldBe(authUser.UserProfile.Pronouns);
        projected.PronounCategory.ShouldBe(authUser.UserProfile.PronounCategory);
        projected.AvatarUrl.ShouldBe(authUser.UserProfile.Avatar);
        projected.IsActive.ShouldBe(isActive);
        projected.CustomFields.ShouldHaveSingleItem().Key.ShouldBe("City");
    }

    [Fact]
    public void ProjectToUserDtoAndUserProfileResultOnDbContextShouldIncludeIsActiveColumnInSql()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
            .Options;

        using var context = new ApplicationDbContext(options);

        // Act
        var userDtoSql = context.Set<AuthUser>().ProjectToType<UserDto>(_config).ToQueryString();
        var userProfileSql = context.Set<AuthUser>().ProjectToType<UserProfileResult>(_config).ToQueryString();

        // Assert
        userDtoSql.ShouldContain("\"IsActive\"");
        userProfileSql.ShouldContain("\"IsActive\"");
    }

    private static AuthUser CreateAuthUser(bool isActive)
    {
        var userId = Guid.NewGuid();

        return new AuthUser
        {
            Id = userId,
            Email = "user@example.com",
            UserName = "user@example.com",
            UserProfile = new User
            {
                Id = userId,
                FirstName = "Olena",
                LastName = "Kovalenko",
                Patronymic = "Ivanivna",
                PhoneNumber = "+380501112233",
                Country = "Ukraine",
                EmergencyContact = "Mom: +380509998877",
                PreferredLanguage = "uk",
                Pronouns = "she/her",
                PronounCategory = PronounCategory.Feminine,
                Avatar = "avatars/user.png",
                IsActive = isActive,
                CustomFields =
                [
                    new UserCustomField
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Key = "City",
                        Value = "Kyiv",
                        IsPublic = true,
                    },
                ],
            },
        };
    }
}
