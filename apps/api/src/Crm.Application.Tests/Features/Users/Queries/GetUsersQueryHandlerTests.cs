using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Role;
using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Queries;
using Crm.Application.Features.Users.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.Users.Queries;

public class GetUsersQueryHandlerTests
{
    private readonly IAuthUserRepository _userRepository = Substitute.For<IAuthUserRepository>();
    private readonly IAuthRoleRepository _authRoleRepository = Substitute.For<IAuthRoleRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IPermissionService _permissionService = Substitute.For<IPermissionService>();
    private readonly IFileUrlProvider _fileUrlProvider = Substitute.For<IFileUrlProvider>();
    private readonly ILogger<GetUsersQueryHandler> _logger = Substitute.For<ILogger<GetUsersQueryHandler>>();

    private readonly GetUsersQueryHandler _handler;

    public GetUsersQueryHandlerTests()
    {
        _handler = new GetUsersQueryHandler(
            _userRepository,
            _authRoleRepository,
            _currentUserService,
            _permissionService,
            _fileUrlProvider,
            _logger);
    }

    [Fact]
    public async Task HandleWhenNoUsersMatchShouldReturnEmptyWithoutFetchingRolesOrPermissions()
    {
        // Arrange
        var query = new GetUsersQuery(PageNumber: 1, PageSize: 10);
        var emptyPagedResult = new PagedResult<UserDto>([], 0, 1, 10);

        _userRepository.GetPagedAsync<UserDto>(
                searchTerm: query.SearchTerm,
                orderBy: query.OrderBy,
                sortOrder: query.SortOrder,
                pageNumber: query.PageNumber,
                pageSize: query.PageSize,
                showDeleted: query.ShowDeleted,
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(emptyPagedResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);

        await _authRoleRepository.DidNotReceive()
            .GetRolesByUserIdsAsync<RoleDto>(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>());
        await _permissionService.DidNotReceive()
            .HasAccessAsync(Arg.Any<List<Guid>>(), Arg.Any<AccessRight>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWhenUsersFoundShouldEnrichRolesAvatarsAndSanitizeEmergencyContactsProperly()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var currentUserRoleIds = new List<Guid> { Guid.NewGuid() };

        _currentUserService.GetUserId().Returns(currentUserId);
        _currentUserService.GetRoleIds().Returns(currentUserRoleIds);

        // Current user does NOT have global ViewEmergencyContact permission
        _permissionService.HasAccessAsync(currentUserRoleIds, AccessRight.ViewEmergencyContact, Arg.Any<CancellationToken>())
            .Returns(false);

        var userSelf = new UserDto(
            Id: currentUserId,
            Email: "self@example.com",
            FirstName: "Olena",
            LastName: "Kovalenko",
            Patronymic: "Ivanivna",
            PhoneNumber: "+380501112233",
            Country: "Ukraine",
            EmergencyContact: "Mom: +380509998877",
            Pronouns: "she/her",
            PronounCategory: PronounCategory.Feminine,
            AvatarUrl: "avatars/self.png",
            Roles: []);

        var userOther = new UserDto(
            Id: otherUserId,
            Email: "other@example.com",
            FirstName: "Taras",
            LastName: "Shevchenko",
            Patronymic: "Hryhorovych",
            PhoneNumber: "+380671112233",
            Country: "Ukraine",
            EmergencyContact: "Brother: +380679998877",
            Pronouns: "he/him",
            PronounCategory: PronounCategory.Masculine,
            AvatarUrl: "avatars/other.png",
            Roles: []);

        var pagedUsers = new PagedResult<UserDto>([userSelf, userOther], 2, 1, 10);

        _userRepository.GetPagedAsync<UserDto>(
                searchTerm: Arg.Any<string?>(),
                orderBy: Arg.Any<string?>(),
                sortOrder: Arg.Any<string?>(),
                pageNumber: 1,
                pageSize: 10,
                showDeleted: false,
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(pagedUsers);

        var adminRole = new RoleDto(Guid.NewGuid(), "Admin", "Adminka", "Admins");
        var volunteerRole = new RoleDto(Guid.NewGuid(), "Volunteer", "Volonterka", "Volunteers");

        // Lookup: userSelf has adminRole, userOther has volunteerRole
        var roleLookup = new[]
        {
            new { UserId = currentUserId, Role = adminRole },
            new { UserId = otherUserId, Role = volunteerRole },
        }.ToLookup(x => x.UserId, x => x.Role);

        _authRoleRepository.GetRolesByUserIdsAsync<RoleDto>(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(roleLookup);

        _fileUrlProvider.GetFileUrl("avatars/self.png", ApiRouteLogoConstants.UserSelfAvatar)
            .Returns("https://api.crm.org/api/v1/users/me/avatar");
        _fileUrlProvider.GetFileUrl("avatars/other.png", ApiRouteLogoConstants.UserAvatar(otherUserId))
            .Returns($"https://api.crm.org/api/v1/users/{otherUserId}/avatar");

        var query = new GetUsersQuery(PageNumber: 1, PageSize: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Count.ShouldBe(2);

        var enrichedSelf = result.Items.First(u => u.Id == currentUserId);
        enrichedSelf.AvatarUrl.ShouldBe("https://api.crm.org/api/v1/users/me/avatar");
        enrichedSelf.EmergencyContact.ShouldBe("Mom: +380509998877"); // Retained because isSelf == true
        enrichedSelf.Roles.Count().ShouldBe(1);
        enrichedSelf.Roles.First().Name.ShouldBe("Adminka"); // Localized using Feminine

        var enrichedOther = result.Items.First(u => u.Id == otherUserId);
        enrichedOther.AvatarUrl.ShouldBe($"https://api.crm.org/api/v1/users/{otherUserId}/avatar");
        enrichedOther.EmergencyContact.ShouldBeNull(); // Masked because not self and no global permission
        enrichedOther.Roles.Count().ShouldBe(1);
        enrichedOther.Roles.First().Name.ShouldBe("Volunteer"); // Localized using Masculine

        // Verify single call to permission service (no N+1 permission calls)
        await _permissionService.Received(1)
            .HasAccessAsync(currentUserRoleIds, AccessRight.ViewEmergencyContact, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWhenUserHasGlobalPermissionShouldExposeEmergencyContactForEveryone()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var currentUserRoleIds = new List<Guid> { Guid.NewGuid() };

        _currentUserService.GetUserId().Returns(currentUserId);
        _currentUserService.GetRoleIds().Returns(currentUserRoleIds);

        // Current user HAS global ViewEmergencyContact permission
        _permissionService.HasAccessAsync(currentUserRoleIds, AccessRight.ViewEmergencyContact, Arg.Any<CancellationToken>())
            .Returns(true);

        var userOther = new UserDto(
            Id: otherUserId,
            Email: "other@example.com",
            FirstName: "Taras",
            LastName: "Shevchenko",
            Patronymic: "Hryhorovych",
            PhoneNumber: "+380671112233",
            Country: "Ukraine",
            EmergencyContact: "Sister: +380679998877",
            Pronouns: "he/him",
            PronounCategory: PronounCategory.Masculine,
            AvatarUrl: null,
            Roles: []);

        var pagedUsers = new PagedResult<UserDto>([userOther], 1, 1, 10);

        _userRepository.GetPagedAsync<UserDto>(
                searchTerm: Arg.Any<string?>(),
                orderBy: Arg.Any<string?>(),
                sortOrder: Arg.Any<string?>(),
                pageNumber: 1,
                pageSize: 10,
                showDeleted: false,
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(pagedUsers);

        var emptyRoleLookup = Enumerable.Empty<RoleDto>().ToLookup(_ => Guid.Empty);
        _authRoleRepository.GetRolesByUserIdsAsync<RoleDto>(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(emptyRoleLookup);

        var query = new GetUsersQuery(PageNumber: 1, PageSize: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var enrichedOther = result.Items.Single();
        enrichedOther.EmergencyContact.ShouldBe("Sister: +380679998877");
    }
}
