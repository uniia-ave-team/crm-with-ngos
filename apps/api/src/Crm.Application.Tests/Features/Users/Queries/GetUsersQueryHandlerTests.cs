using System.Text.Json.Nodes;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Roles;
using Crm.Application.Dtos.Users;
using Crm.Application.Features.Users.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.Users.Queries;

public class GetUsersQueryHandlerTests
{
    private static readonly string[] CityOnlyKeys = ["city"];
    private static readonly string[] SecretOnlyKeys = ["secret"];

    private readonly IAuthUserRepository _userRepository = Substitute.For<IAuthUserRepository>();
    private readonly IAuthRoleRepository _authRoleRepository = Substitute.For<IAuthRoleRepository>();
    private readonly IUserCustomFieldRepository _userCustomFieldRepository = Substitute.For<IUserCustomFieldRepository>();
    private readonly IPermissionService _permissionService = Substitute.For<IPermissionService>();
    private readonly IFileUrlProvider _fileUrlProvider = Substitute.For<IFileUrlProvider>();
    private readonly ILogger<GetUsersQueryHandler> _logger = Substitute.For<ILogger<GetUsersQueryHandler>>();

    private readonly GetUsersQueryHandler _handler;

    public GetUsersQueryHandlerTests()
    {
        _userCustomFieldRepository.GetAllOrderedAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        _handler = new GetUsersQueryHandler(
            _userRepository,
            _authRoleRepository,
            _userCustomFieldRepository,
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
                customFieldSort: null,
                searchablePublicCustomFieldKeys: Arg.Any<IReadOnlyList<string>?>(),
                searchablePrivateCustomFieldKeys: Arg.Any<IReadOnlyList<string>?>(),
                currentUserId: query.CurrentUserId,
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
    public async Task HandleWhenUsersFoundShouldEnrichRolesAvatarsCustomFieldsAndSanitizeSensitiveDataProperly()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var currentUserRoleIds = new List<Guid> { Guid.NewGuid() };

        _userCustomFieldRepository.GetAllOrderedAsync(Arg.Any<CancellationToken>())
            .Returns(
            [
                new UserCustomField { Id = Guid.CreateVersion7(), Name = "City", Key = "city", DataType = FieldDataType.Text, IsPublic = true, Order = 0 },
                new UserCustomField { Id = Guid.CreateVersion7(), Name = "Salary", Key = "salary", DataType = FieldDataType.Number, IsPublic = false, Order = 1 },
            ]);

        _permissionService.HasAccessAsync(currentUserRoleIds, AccessRight.ViewEmergencyContact, Arg.Any<CancellationToken>())
            .Returns(false);
        _permissionService.HasAccessAsync(currentUserRoleIds, AccessRight.ViewCustomFields, Arg.Any<CancellationToken>())
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
            IsActive: true,
            Roles: [],
            CustomFields: new JsonObject { ["city"] = "Kyiv", ["salary"] = 4500 });

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
            IsActive: true,
            Roles: [],
            CustomFields: new JsonObject { ["city"] = "Lviv", ["salary"] = 5000 });

        var pagedUsers = new PagedResult<UserDto>([userSelf, userOther], 2, 1, 10);

        _userRepository.GetPagedAsync<UserDto>(
                searchTerm: Arg.Any<string?>(),
                orderBy: Arg.Any<string?>(),
                sortOrder: Arg.Any<string?>(),
                pageNumber: 1,
                pageSize: 10,
                showDeleted: false,
                customFieldSort: Arg.Any<UserCustomFieldSort?>(),
                searchablePublicCustomFieldKeys: Arg.Any<IReadOnlyList<string>?>(),
                searchablePrivateCustomFieldKeys: Arg.Any<IReadOnlyList<string>?>(),
                currentUserId: Arg.Any<Guid?>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(pagedUsers);

        var adminRole = new RoleDto(Guid.NewGuid(), "Admin", "Adminka", "Admins");
        var volunteerRole = new RoleDto(Guid.NewGuid(), "Volunteer", "Volonterka", "Volunteers");

        var roleLookup = new[]
        {
            new { UserId = currentUserId, Role = adminRole },
            new { UserId = otherUserId, Role = volunteerRole },
        }.ToLookup(x => x.UserId, x => x.Role);

        _authRoleRepository.GetRolesByUserIdsAsync<RoleDto>(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(roleLookup);

        _fileUrlProvider.GetFileUrl("avatars/self.png", FileRouteConstants.UserSelfAvatar)
            .Returns("https://api.crm.org/api/v1/users/me/avatar");
        _fileUrlProvider.GetFileUrl("avatars/other.png", FileRouteConstants.UserAvatar(otherUserId))
            .Returns($"https://api.crm.org/api/v1/users/{otherUserId}/avatar");

        var query = new GetUsersQuery(PageNumber: 1, PageSize: 10)
        {
            CurrentUserId = currentUserId,
            CurrentUserRoleIds = currentUserRoleIds,
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Count.ShouldBe(2);

        var enrichedSelf = result.Items.First(u => u.Id == currentUserId);
        enrichedSelf.AvatarUrl.ShouldBe("https://api.crm.org/api/v1/users/me/avatar");
        enrichedSelf.EmergencyContact.ShouldBe("Mom: +380509998877");
        enrichedSelf.IsActive.ShouldBeTrue();
        enrichedSelf.Roles.Count().ShouldBe(1);
        enrichedSelf.Roles.First().Name.ShouldBe("Adminka");
        enrichedSelf.CustomFields.Select(kvp => kvp.Key).ShouldBe(["city", "salary"]);

        var enrichedOther = result.Items.First(u => u.Id == otherUserId);
        enrichedOther.AvatarUrl.ShouldBe($"https://api.crm.org/api/v1/users/{otherUserId}/avatar");
        enrichedOther.EmergencyContact.ShouldBeNull();
        enrichedOther.IsActive.ShouldBeTrue();
        enrichedOther.Roles.Count().ShouldBe(1);
        enrichedOther.Roles.First().Name.ShouldBe("Volunteer");
        enrichedOther.CustomFields.Select(kvp => kvp.Key).ShouldBe(["city"]);

        await _permissionService.Received(1)
            .HasAccessAsync(currentUserRoleIds, AccessRight.ViewEmergencyContact, Arg.Any<CancellationToken>());
        await _permissionService.Received(1)
            .HasAccessAsync(currentUserRoleIds, AccessRight.ViewCustomFields, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWhenUserHasGlobalPermissionShouldExposeEmergencyContactAndPrivateCustomFieldsForEveryone()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var currentUserRoleIds = new List<Guid> { Guid.NewGuid() };

        _userCustomFieldRepository.GetAllOrderedAsync(Arg.Any<CancellationToken>())
            .Returns(
            [
                new UserCustomField { Id = Guid.CreateVersion7(), Name = "Salary", Key = "salary", DataType = FieldDataType.Number, IsPublic = false, Order = 0 },
            ]);

        _permissionService.HasAccessAsync(currentUserRoleIds, AccessRight.ViewEmergencyContact, Arg.Any<CancellationToken>())
            .Returns(true);
        _permissionService.HasAccessAsync(currentUserRoleIds, AccessRight.ViewCustomFields, Arg.Any<CancellationToken>())
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
            IsActive: true,
            Roles: [],
            CustomFields: new JsonObject { ["salary"] = 5000 });

        var pagedUsers = new PagedResult<UserDto>([userOther], 1, 1, 10);

        _userRepository.GetPagedAsync<UserDto>(
                searchTerm: Arg.Any<string?>(),
                orderBy: Arg.Any<string?>(),
                sortOrder: Arg.Any<string?>(),
                pageNumber: 1,
                pageSize: 10,
                showDeleted: false,
                customFieldSort: Arg.Any<UserCustomFieldSort?>(),
                searchablePublicCustomFieldKeys: Arg.Any<IReadOnlyList<string>?>(),
                searchablePrivateCustomFieldKeys: Arg.Any<IReadOnlyList<string>?>(),
                currentUserId: Arg.Any<Guid?>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(pagedUsers);

        var emptyRoleLookup = Enumerable.Empty<RoleDto>().ToLookup(_ => Guid.Empty);
        _authRoleRepository.GetRolesByUserIdsAsync<RoleDto>(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(emptyRoleLookup);

        var query = new GetUsersQuery(PageNumber: 1, PageSize: 10)
        {
            CurrentUserId = currentUserId,
            CurrentUserRoleIds = currentUserRoleIds,
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var enrichedOther = result.Items.Single();
        enrichedOther.EmergencyContact.ShouldBe("Sister: +380679998877");
        enrichedOther.IsActive.ShouldBeTrue();
        enrichedOther.CustomFields.Select(kvp => kvp.Key).ShouldBe(["salary"]);
    }

    [Fact]
    public async Task HandleWhenShowDeletedIsTrueShouldForwardFlagAndPreserveIsActiveStatus()
    {
        // Arrange
        var activeUserId = Guid.NewGuid();
        var inactiveUserId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var currentUserRoleIds = new List<Guid> { Guid.NewGuid() };

        _permissionService.HasAccessAsync(currentUserRoleIds, AccessRight.ViewEmergencyContact, Arg.Any<CancellationToken>())
            .Returns(false);

        var activeUser = new UserDto(
            Id: activeUserId,
            Email: "active@example.com",
            FirstName: "Active",
            LastName: "User",
            Patronymic: null,
            PhoneNumber: null,
            Country: null,
            EmergencyContact: null,
            Pronouns: null,
            PronounCategory: null,
            AvatarUrl: null,
            IsActive: true,
            Roles: [],
            CustomFields: []);

        var inactiveUser = new UserDto(
            Id: inactiveUserId,
            Email: "inactive@example.com",
            FirstName: "Inactive",
            LastName: "User",
            Patronymic: null,
            PhoneNumber: null,
            Country: null,
            EmergencyContact: null,
            Pronouns: null,
            PronounCategory: null,
            AvatarUrl: null,
            IsActive: false,
            Roles: [],
            CustomFields: []);

        var pagedUsers = new PagedResult<UserDto>([activeUser, inactiveUser], 2, 1, 10);

        _userRepository.GetPagedAsync<UserDto>(
                searchTerm: Arg.Any<string?>(),
                orderBy: Arg.Any<string?>(),
                sortOrder: Arg.Any<string?>(),
                pageNumber: 1,
                pageSize: 10,
                showDeleted: true,
                customFieldSort: Arg.Any<UserCustomFieldSort?>(),
                searchablePublicCustomFieldKeys: Arg.Any<IReadOnlyList<string>?>(),
                searchablePrivateCustomFieldKeys: Arg.Any<IReadOnlyList<string>?>(),
                currentUserId: Arg.Any<Guid?>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(pagedUsers);

        var emptyRoleLookup = Enumerable.Empty<RoleDto>().ToLookup(_ => Guid.Empty);
        _authRoleRepository.GetRolesByUserIdsAsync<RoleDto>(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(emptyRoleLookup);

        var query = new GetUsersQuery(PageNumber: 1, PageSize: 10, ShowDeleted: true)
        {
            CurrentUserId = currentUserId,
            CurrentUserRoleIds = currentUserRoleIds,
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.Items.Single(u => u.Id == activeUserId).IsActive.ShouldBeTrue();
        result.Items.Single(u => u.Id == inactiveUserId).IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleWhenOrderByIsEmergencyContactAndCallerLacksPermissionShouldPassNullOrderByToRepository()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var currentUserRoleIds = new List<Guid> { Guid.NewGuid() };

        _permissionService.HasAccessAsync(currentUserRoleIds, AccessRight.ViewEmergencyContact, Arg.Any<CancellationToken>())
            .Returns(false);

        var emptyPagedResult = new PagedResult<UserDto>([], 0, 1, 10);

        _userRepository.GetPagedAsync<UserDto>(
                searchTerm: Arg.Any<string?>(),
                orderBy: Arg.Is<string?>(x => x == null),
                sortOrder: Arg.Any<string?>(),
                pageNumber: 1,
                pageSize: 10,
                showDeleted: false,
                customFieldSort: null,
                searchablePublicCustomFieldKeys: Arg.Any<IReadOnlyList<string>?>(),
                searchablePrivateCustomFieldKeys: Arg.Any<IReadOnlyList<string>?>(),
                currentUserId: currentUserId,
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(emptyPagedResult);

        var query = new GetUsersQuery(OrderBy: " emergencycontact ", PageNumber: 1, PageSize: 10)
        {
            CurrentUserId = currentUserId,
            CurrentUserRoleIds = currentUserRoleIds,
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.ShouldBeEmpty();
        await _userRepository.Received(1).GetPagedAsync<UserDto>(
            searchTerm: null,
            orderBy: null,
            sortOrder: query.SortOrder,
            pageNumber: 1,
            pageSize: 10,
            showDeleted: false,
            customFieldSort: null,
            searchablePublicCustomFieldKeys: Arg.Any<IReadOnlyList<string>?>(),
            searchablePrivateCustomFieldKeys: Arg.Any<IReadOnlyList<string>?>(),
            currentUserId: currentUserId,
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWhenOrderByIsEmergencyContactAndCallerHasPermissionShouldPreserveOrderBy()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var currentUserRoleIds = new List<Guid> { Guid.NewGuid() };

        _permissionService.HasAccessAsync(currentUserRoleIds, AccessRight.ViewEmergencyContact, Arg.Any<CancellationToken>())
            .Returns(true);

        var emptyPagedResult = new PagedResult<UserDto>([], 0, 1, 10);

        _userRepository.GetPagedAsync<UserDto>(
                searchTerm: Arg.Any<string?>(),
                orderBy: nameof(UserDto.EmergencyContact),
                sortOrder: Arg.Any<string?>(),
                pageNumber: 1,
                pageSize: 10,
                showDeleted: false,
                customFieldSort: null,
                searchablePublicCustomFieldKeys: Arg.Any<IReadOnlyList<string>?>(),
                searchablePrivateCustomFieldKeys: Arg.Any<IReadOnlyList<string>?>(),
                currentUserId: currentUserId,
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(emptyPagedResult);

        var query = new GetUsersQuery(OrderBy: nameof(UserDto.EmergencyContact), PageNumber: 1, PageSize: 10)
        {
            CurrentUserId = currentUserId,
            CurrentUserRoleIds = currentUserRoleIds,
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.ShouldBeEmpty();
        await _userRepository.Received(1).GetPagedAsync<UserDto>(
            searchTerm: null,
            orderBy: nameof(UserDto.EmergencyContact),
            sortOrder: query.SortOrder,
            pageNumber: 1,
            pageSize: 10,
            showDeleted: false,
            customFieldSort: null,
            searchablePublicCustomFieldKeys: Arg.Any<IReadOnlyList<string>?>(),
            searchablePrivateCustomFieldKeys: Arg.Any<IReadOnlyList<string>?>(),
            currentUserId: currentUserId,
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("age")]
    [InlineData("customFields.age")]
    [InlineData("customFields['age']")]
    public async Task HandleWhenOrderByIsBareOrPrefixedCustomFieldShouldResolveCanonicalKeyAndDataType(string orderBy)
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var currentUserRoleIds = new List<Guid> { Guid.NewGuid() };

        _permissionService.HasAccessAsync(currentUserRoleIds, AccessRight.ViewCustomFields, Arg.Any<CancellationToken>())
            .Returns(true);

        _userCustomFieldRepository.GetAllOrderedAsync(Arg.Any<CancellationToken>())
            .Returns([new UserCustomField { Id = Guid.CreateVersion7(), Name = "Age", Key = "age", DataType = FieldDataType.Number, IsPublic = true, Order = 0 }]);

        _userRepository.GetPagedAsync<UserDto>(
                searchTerm: null,
                orderBy: null,
                sortOrder: "desc",
                pageNumber: 1,
                pageSize: 10,
                showDeleted: false,
                customFieldSort: Arg.Is<UserCustomFieldSort?>(s => s != null && s.Key == "age" && s.DataType == FieldDataType.Number),
                searchablePublicCustomFieldKeys: Arg.Any<IReadOnlyList<string>?>(),
                searchablePrivateCustomFieldKeys: Arg.Any<IReadOnlyList<string>?>(),
                currentUserId: currentUserId,
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new PagedResult<UserDto>([], 0, 1, 10));

        var query = new GetUsersQuery(OrderBy: orderBy, SortOrder: "desc")
        {
            CurrentUserId = currentUserId,
            CurrentUserRoleIds = currentUserRoleIds,
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.ShouldBeEmpty();
        await _userRepository.Received(1).GetPagedAsync<UserDto>(
            searchTerm: null,
            orderBy: null,
            sortOrder: "desc",
            pageNumber: 1,
            pageSize: 10,
            showDeleted: false,
            customFieldSort: Arg.Is<UserCustomFieldSort?>(s => s != null && s.Key == "age" && s.DataType == FieldDataType.Number),
            searchablePublicCustomFieldKeys: Arg.Any<IReadOnlyList<string>?>(),
            searchablePrivateCustomFieldKeys: Arg.Any<IReadOnlyList<string>?>(),
            currentUserId: currentUserId,
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWhenSearchTermProvidedShouldPartitionSearchableKeysByVisibility()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var currentUserRoleIds = new List<Guid> { Guid.NewGuid() };

        _userCustomFieldRepository.GetAllOrderedAsync(Arg.Any<CancellationToken>())
            .Returns(
            [
                new UserCustomField { Id = Guid.CreateVersion7(), Name = "City", Key = "city", DataType = FieldDataType.Text, IsPublic = true, Order = 0 },
                new UserCustomField { Id = Guid.CreateVersion7(), Name = "Secret", Key = "secret", DataType = FieldDataType.Text, IsPublic = false, Order = 1 },
            ]);

        _permissionService.HasAccessAsync(currentUserRoleIds, AccessRight.ViewCustomFields, Arg.Any<CancellationToken>())
            .Returns(false);

        _userRepository.GetPagedAsync<UserDto>(
                searchTerm: "Kyiv",
                orderBy: nameof(User.FirstName),
                sortOrder: Domain.Constants.SortOrderConstants.Ascending,
                pageNumber: 1,
                pageSize: 10,
                showDeleted: false,
                customFieldSort: null,
                searchablePublicCustomFieldKeys: Arg.Is<IReadOnlyList<string>?>(k => k != null && k.SequenceEqual(CityOnlyKeys)),
                searchablePrivateCustomFieldKeys: Arg.Is<IReadOnlyList<string>?>(k => k != null && k.SequenceEqual(SecretOnlyKeys)),
                currentUserId: currentUserId,
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new PagedResult<UserDto>([], 0, 1, 10));

        var query = new GetUsersQuery(SearchTerm: "Kyiv")
        {
            CurrentUserId = currentUserId,
            CurrentUserRoleIds = currentUserRoleIds,
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.ShouldBeEmpty();
        await _permissionService.Received(1)
            .HasAccessAsync(currentUserRoleIds, AccessRight.ViewCustomFields, Arg.Any<CancellationToken>());
    }
}
