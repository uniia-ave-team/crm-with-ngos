using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Role;
using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Queries;
using Crm.Application.Features.Users.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.Users.Queries;

public class GetUserProfileQueryHandlerTests
{
    private readonly IAuthUserRepository _authUserRepository = Substitute.For<IAuthUserRepository>();
    private readonly IAuthRoleRepository _authRoleRepository = Substitute.For<IAuthRoleRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IPermissionService _permissionService = Substitute.For<IPermissionService>();
    private readonly IFileUrlProvider _fileUrlProvider = Substitute.For<IFileUrlProvider>();
    private readonly ILogger<GetUserProfileQueryHandler> _logger = Substitute.For<ILogger<GetUserProfileQueryHandler>>();

    private readonly GetUserProfileQueryHandler _handler;

    public GetUserProfileQueryHandlerTests()
    {
        _handler = new GetUserProfileQueryHandler(
            _authUserRepository,
            _authRoleRepository,
            _currentUserService,
            _permissionService,
            _fileUrlProvider,
            _logger);
    }

    [Fact]
    public async Task HandleWhenUserSelfShouldEnrichRolesAvatarsAndPreserveAllData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var query = new GetUserProfileQuery(userId, IsSelf: true);

        _currentUserService.GetUserId().Returns(userId);
        _currentUserService.GetRoleIds().Returns([roleId]);

        var userResult = new UserProfileResult(
            Id: userId,
            Email: "self@example.com",
            FirstName: "Olena",
            LastName: "Kovalenko",
            Patronymic: "Ivanivna",
            PhoneNumber: "+380501112233",
            Country: "Ukraine",
            EmergencyContact: "Mom: +380509998877",
            PreferredLanguage: "uk",
            Pronouns: "she/her",
            PronounCategory: PronounCategory.Feminine,
            AvatarUrl: "avatars/self.png",
            IsActive: true,
            Roles: [],
            CustomFields:
            [
                new UserCustomFieldDto(Guid.NewGuid(), "PublicBio", "Hello", IsPublic: true),
                new UserCustomFieldDto(Guid.NewGuid(), "SecretNote", "Private", IsPublic: false),
            ]);

        var roles = new List<RoleDto> { new(roleId, "Volunteer", "Volonterka", "Volunteers") };

        _authUserRepository.GetAsync<UserProfileResult>(userId, Arg.Any<CancellationToken>())
            .Returns(userResult);

        _authRoleRepository.GetRolesByUserAsync<RoleDto>(userId, Arg.Any<CancellationToken>())
            .Returns(roles);

        _fileUrlProvider.GetFileUrl("avatars/self.png", ApiRouteLogoConstants.UserSelfAvatar)
            .Returns("https://api.crm.org/api/v1/users/me/avatar");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Id.ShouldBe(userId);
        result.Email.ShouldBe("self@example.com");
        result.FirstName.ShouldBe("Olena");
        result.EmergencyContact.ShouldBe("Mom: +380509998877");
        result.AvatarUrl.ShouldBe("https://api.crm.org/api/v1/users/me/avatar");
        result.IsActive.ShouldBeTrue();
        result.Roles.Single().Name.ShouldBe("Volonterka");
        result.CustomFields.Count().ShouldBe(2);

        await _permissionService.DidNotReceive()
            .HasAccessAsync(Arg.Any<List<Guid>>(), Arg.Any<AccessRight>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWhenUserIdMatchesCurrentUserIdEvenWithIsSelfFalseShouldTreatAsSelf()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var query = new GetUserProfileQuery(userId, IsSelf: false);

        _currentUserService.GetUserId().Returns(userId);

        var userResult = new UserProfileResult(
            Id: userId,
            Email: "self@example.com",
            FirstName: "Olena",
            LastName: "Kovalenko",
            Patronymic: "Ivanivna",
            PhoneNumber: "+380501112233",
            Country: "Ukraine",
            EmergencyContact: "Mom: +380509998877",
            PreferredLanguage: "uk",
            Pronouns: "she/her",
            PronounCategory: PronounCategory.Feminine,
            AvatarUrl: "avatars/self.png",
            IsActive: true,
            Roles: [],
            CustomFields:
            [
                new UserCustomFieldDto(Guid.NewGuid(), "PublicBio", "Hello", IsPublic: true),
                new UserCustomFieldDto(Guid.NewGuid(), "SecretNote", "Private", IsPublic: false),
            ]);

        var roles = new List<RoleDto> { new(roleId, "Volunteer", "Volonterka", "Volunteers") };

        _authUserRepository.GetAsync<UserProfileResult>(userId, Arg.Any<CancellationToken>())
            .Returns(userResult);

        _authRoleRepository.GetRolesByUserAsync<RoleDto>(userId, Arg.Any<CancellationToken>())
            .Returns(roles);

        _fileUrlProvider.GetFileUrl("avatars/self.png", ApiRouteLogoConstants.UserSelfAvatar)
            .Returns("https://api.crm.org/api/v1/users/me/avatar");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.EmergencyContact.ShouldBe("Mom: +380509998877");
        result.CustomFields.Count().ShouldBe(2);

        await _permissionService.DidNotReceive()
            .HasAccessAsync(Arg.Any<List<Guid>>(), Arg.Any<AccessRight>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWhenNotSelfAndNoPermissionsShouldMaskEmergencyContactAndFilterPrivateCustomFields()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var currentUserRoleIds = new List<Guid> { Guid.NewGuid() };
        var query = new GetUserProfileQuery(targetUserId, IsSelf: false);

        _currentUserService.GetUserId().Returns(currentUserId);
        _currentUserService.GetRoleIds().Returns(currentUserRoleIds);

        _permissionService.HasAccessAsync(currentUserRoleIds, AccessRight.ViewEmergencyContact, Arg.Any<CancellationToken>())
            .Returns(false);
        _permissionService.HasAccessAsync(currentUserRoleIds, AccessRight.ViewCustomFields, Arg.Any<CancellationToken>())
            .Returns(false);

        var userResult = new UserProfileResult(
            Id: targetUserId,
            Email: "target@example.com",
            FirstName: "Petro",
            LastName: "Petrenko",
            Patronymic: "Petrovych",
            PhoneNumber: "+380502223344",
            Country: "Ukraine",
            EmergencyContact: "Spouse: +380501112233",
            PreferredLanguage: "uk",
            Pronouns: "he/him",
            PronounCategory: PronounCategory.Masculine,
            AvatarUrl: "avatars/target.png",
            IsActive: true,
            Roles: [],
            CustomFields:
            [
                new UserCustomFieldDto(Guid.NewGuid(), "PublicBio", "Public Info", IsPublic: true),
                new UserCustomFieldDto(Guid.NewGuid(), "InternalNotes", "Top Secret", IsPublic: false),
            ]);

        var roles = new List<RoleDto> { new(Guid.NewGuid(), "Coordinator", "Koordynatorka", "Coordinators") };

        _authUserRepository.GetAsync<UserProfileResult>(targetUserId, Arg.Any<CancellationToken>())
            .Returns(userResult);

        _authRoleRepository.GetRolesByUserAsync<RoleDto>(targetUserId, Arg.Any<CancellationToken>())
            .Returns(roles);

        _fileUrlProvider.GetFileUrl("avatars/target.png", ApiRouteLogoConstants.UserAvatar(targetUserId))
            .Returns($"https://api.crm.org/api/v1/users/{targetUserId}/avatar");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.EmergencyContact.ShouldBeNull();
        result.AvatarUrl.ShouldBe($"https://api.crm.org/api/v1/users/{targetUserId}/avatar");
        result.Roles.Single().Name.ShouldBe("Coordinator");
        result.CustomFields.ShouldHaveSingleItem().Key.ShouldBe("PublicBio");
    }

    [Fact]
    public async Task HandleWhenNotSelfAndHasGlobalPermissionsShouldIncludeAllSensitiveData()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var currentUserRoleIds = new List<Guid> { Guid.NewGuid() };
        var query = new GetUserProfileQuery(targetUserId, IsSelf: false);

        _currentUserService.GetUserId().Returns(currentUserId);
        _currentUserService.GetRoleIds().Returns(currentUserRoleIds);

        _permissionService.HasAccessAsync(currentUserRoleIds, AccessRight.ViewEmergencyContact, Arg.Any<CancellationToken>())
            .Returns(true);
        _permissionService.HasAccessAsync(currentUserRoleIds, AccessRight.ViewCustomFields, Arg.Any<CancellationToken>())
            .Returns(true);

        var userResult = new UserProfileResult(
            Id: targetUserId,
            Email: "target@example.com",
            FirstName: "Petro",
            LastName: "Petrenko",
            Patronymic: null,
            PhoneNumber: null,
            Country: null,
            EmergencyContact: "Spouse: +380501112233",
            PreferredLanguage: "en",
            Pronouns: null,
            PronounCategory: null,
            AvatarUrl: null,
            IsActive: true,
            Roles: [],
            CustomFields:
            [
                new UserCustomFieldDto(Guid.NewGuid(), "PublicBio", "Public Info", IsPublic: true),
                new UserCustomFieldDto(Guid.NewGuid(), "InternalNotes", "Top Secret", IsPublic: false),
            ]);

        _authUserRepository.GetAsync<UserProfileResult>(targetUserId, Arg.Any<CancellationToken>())
            .Returns(userResult);

        _authRoleRepository.GetRolesByUserAsync<RoleDto>(targetUserId, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.EmergencyContact.ShouldBe("Spouse: +380501112233");
        result.AvatarUrl.ShouldBeNull();
        result.IsActive.ShouldBeTrue();
        result.CustomFields.Count().ShouldBe(2);
    }

    [Fact]
    public async Task HandleWhenUserIsDeactivatedShouldReturnProfileWithIsActiveFalse()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var currentUserRoleIds = new List<Guid> { Guid.NewGuid() };
        var query = new GetUserProfileQuery(targetUserId, IsSelf: false);

        _currentUserService.GetUserId().Returns(currentUserId);
        _currentUserService.GetRoleIds().Returns(currentUserRoleIds);

        var userResult = new UserProfileResult(
            Id: targetUserId,
            Email: "inactive@example.com",
            FirstName: "Ivan",
            LastName: "Franko",
            Patronymic: null,
            PhoneNumber: null,
            Country: null,
            EmergencyContact: null,
            PreferredLanguage: "uk",
            Pronouns: null,
            PronounCategory: null,
            AvatarUrl: null,
            IsActive: false,
            Roles: [],
            CustomFields: []);

        _authUserRepository.GetAsync<UserProfileResult>(targetUserId, Arg.Any<CancellationToken>())
            .Returns(userResult);

        _authRoleRepository.GetRolesByUserAsync<RoleDto>(targetUserId, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Id.ShouldBe(targetUserId);
        result.IsActive.ShouldBeFalse();
    }
}
