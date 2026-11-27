using System.Text.Json.Nodes;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Roles;
using Crm.Application.Dtos.Users;
using Crm.Application.Features.Users.Mappings;
using Crm.Application.Features.Users.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.Users.Queries;

public class GetUserProfileQueryHandlerTests
{
    private static readonly Guid PublicBioFieldId = Guid.Parse("019a0000-0000-7000-8000-000000000101");
    private static readonly Guid SecretNoteFieldId = Guid.Parse("019a0000-0000-7000-8000-000000000102");
    private static readonly string PublicBioKey = PublicBioFieldId.ToString();
    private static readonly string SecretNoteKey = SecretNoteFieldId.ToString();

    private readonly IAuthUserRepository _authUserRepository = Substitute.For<IAuthUserRepository>();
    private readonly IAuthRoleRepository _authRoleRepository = Substitute.For<IAuthRoleRepository>();
    private readonly IUserCustomFieldRepository _userCustomFieldRepository = Substitute.For<IUserCustomFieldRepository>();
    private readonly IPermissionService _permissionService = Substitute.For<IPermissionService>();
    private readonly IFileUrlProvider _fileUrlProvider = Substitute.For<IFileUrlProvider>();
    private readonly ILogger<GetUserProfileQueryHandler> _logger = Substitute.For<ILogger<GetUserProfileQueryHandler>>();

    private readonly GetUserProfileQueryHandler _handler;

    static GetUserProfileQueryHandlerTests()
    {
        new UserMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
    }

    public GetUserProfileQueryHandlerTests()
    {
        _userCustomFieldRepository.GetAllOrderedAsync(Arg.Any<CancellationToken>())
            .Returns(
            [
                new UserCustomField { Id = PublicBioFieldId, Name = "Public Bio", IsPublic = true, Order = 0 },
                new UserCustomField { Id = SecretNoteFieldId, Name = "Secret Note", IsPublic = false, Order = 1 },
            ]);

        _handler = new GetUserProfileQueryHandler(
            _authUserRepository,
            _authRoleRepository,
            _userCustomFieldRepository,
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
        var query = new GetUserProfileQuery(userId, IsSelf: true)
        {
            CurrentUserId = userId,
            CurrentUserRoleIds = [roleId],
        };

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
            CustomFields: new JsonObject
            {
                [PublicBioKey] = "Hello",
                [SecretNoteKey] = "Private",
            });

        var roles = new List<RoleDto> { new(roleId, "Volunteer", "Volonterka", "Volunteers") };

        _authUserRepository.GetAsync<UserProfileResult>(userId, Arg.Any<CancellationToken>())
            .Returns(userResult);

        _authRoleRepository.GetRolesByUserAsync<RoleDto>(userId, Arg.Any<CancellationToken>())
            .Returns(roles);

        _fileUrlProvider.GetFileUrl("avatars/self.png", FileRouteConstants.UserSelfAvatar)
            .Returns("https://api.crm.org/api/v1/users/me/avatar");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Id.ShouldBe(userId);
        result.Email.ShouldBe("self@example.com");
        result.FirstName.ShouldBe("Olena");
        result.Pronouns.ShouldBe("she/her");
        result.PronounCategory.ShouldBe(PronounCategory.Feminine);
        result.EmergencyContact.ShouldBe("Mom: +380509998877");
        result.AvatarUrl.ShouldBe("https://api.crm.org/api/v1/users/me/avatar");
        result.IsActive.ShouldBeTrue();
        result.Roles.Single().Name.ShouldBe("Volonterka");
        result.CustomFields.Select(kvp => kvp.Key).ShouldBe([PublicBioKey, SecretNoteKey]);

        await _permissionService.DidNotReceive()
            .HasAccessAsync(Arg.Any<List<Guid>>(), Arg.Any<AccessRight>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWhenUserIdMatchesCurrentUserIdEvenWithIsSelfFalseShouldTreatAsSelf()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var query = new GetUserProfileQuery(userId, IsSelf: false)
        {
            CurrentUserId = userId,
            CurrentUserRoleIds = [],
        };

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
            CustomFields: new JsonObject
            {
                [PublicBioKey] = "Hello",
                [SecretNoteKey] = "Private",
            });

        var roles = new List<RoleDto> { new(roleId, "Volunteer", "Volonterka", "Volunteers") };

        _authUserRepository.GetAsync<UserProfileResult>(userId, Arg.Any<CancellationToken>())
            .Returns(userResult);

        _authRoleRepository.GetRolesByUserAsync<RoleDto>(userId, Arg.Any<CancellationToken>())
            .Returns(roles);

        _fileUrlProvider.GetFileUrl("avatars/self.png", FileRouteConstants.UserSelfAvatar)
            .Returns("https://api.crm.org/api/v1/users/me/avatar");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.EmergencyContact.ShouldBe("Mom: +380509998877");
        result.CustomFields.Count.ShouldBe(2);

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
        var query = new GetUserProfileQuery(targetUserId, IsSelf: false)
        {
            CurrentUserId = currentUserId,
            CurrentUserRoleIds = currentUserRoleIds,
        };

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
            CustomFields: new JsonObject
            {
                [PublicBioKey] = "Public Info",
                [SecretNoteKey] = "Top Secret",
            });

        var roles = new List<RoleDto> { new(Guid.NewGuid(), "Coordinator", "Koordynatorka", "Coordinators") };

        _authUserRepository.GetAsync<UserProfileResult>(targetUserId, Arg.Any<CancellationToken>())
            .Returns(userResult);

        _authRoleRepository.GetRolesByUserAsync<RoleDto>(targetUserId, Arg.Any<CancellationToken>())
            .Returns(roles);

        _fileUrlProvider.GetFileUrl("avatars/target.png", FileRouteConstants.UserAvatar(targetUserId))
            .Returns($"https://api.crm.org/api/v1/users/{targetUserId}/avatar");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.EmergencyContact.ShouldBeNull();
        result.AvatarUrl.ShouldBe($"https://api.crm.org/api/v1/users/{targetUserId}/avatar");
        result.Roles.Single().Name.ShouldBe("Coordinator");
        result.CustomFields.Select(kvp => kvp.Key).ShouldBe([PublicBioKey]);
    }

    [Fact]
    public async Task HandleWhenNotSelfAndHasGlobalPermissionsShouldIncludeAllSensitiveData()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var currentUserRoleIds = new List<Guid> { Guid.NewGuid() };
        var query = new GetUserProfileQuery(targetUserId, IsSelf: false)
        {
            CurrentUserId = currentUserId,
            CurrentUserRoleIds = currentUserRoleIds,
        };

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
            CustomFields: new JsonObject
            {
                [PublicBioKey] = "Public Info",
                [SecretNoteKey] = "Top Secret",
            });

        _authUserRepository.GetAsync<UserProfileResult>(targetUserId, Arg.Any<CancellationToken>())
            .Returns(userResult);

        _authRoleRepository.GetRolesByUserAsync<RoleDto>(targetUserId, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.EmergencyContact.ShouldBe("Spouse: +380501112233");
        result.PronounCategory.ShouldBeNull();
        result.AvatarUrl.ShouldBeNull();
        result.IsActive.ShouldBeTrue();
        result.CustomFields.Count.ShouldBe(2);
    }

    [Fact]
    public async Task HandleWhenUserIsDeactivatedShouldReturnProfileWithIsActiveFalse()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var currentUserRoleIds = new List<Guid> { Guid.NewGuid() };
        var query = new GetUserProfileQuery(targetUserId, IsSelf: false)
        {
            CurrentUserId = currentUserId,
            CurrentUserRoleIds = currentUserRoleIds,
        };

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
