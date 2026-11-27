using Crm.Application.Features.Users.Commands;
using Crm.Application.Features.Users.Mappings;
using Crm.Application.Interfaces;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.Users.Commands;

/// <summary>
/// Unit tests verifying <see cref="UpdateUserProfileCommandHandler"/> profile updates
/// and <see cref="AccessRight.ViewEmergencyContact"/> write protection.
/// </summary>
public class UpdateUserProfileCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPermissionService _permissionService = Substitute.For<IPermissionService>();
    private readonly UpdateUserProfileCommandHandler _handler;

    static UpdateUserProfileCommandHandlerTests()
    {
        new UserMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
    }

    public UpdateUserProfileCommandHandlerTests()
    {
        _handler = new UpdateUserProfileCommandHandler(
            _userRepository,
            _permissionService,
            Substitute.For<ILogger<UpdateUserProfileCommandHandler>>());
    }

    [Theory]
    [InlineData("Dad: +380501234567")]
    [InlineData(null)]
    public async Task HandleWhenUpdatingSelfProfileShouldUpdateOrClearEmergencyContactWithoutPermissionCheck(
        string? newEmergencyContact)
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var user = CreateUser(userId, emergencyContact: "Mom: +380509998877");

        _userRepository.GetForUpdateAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var command = CreateCommand(newEmergencyContact) with
        {
            UserId = userId,
            CurrentUserId = userId,
            TargetCurrentUser = true,
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        user.FirstName.ShouldBe("UpdatedFirst");
        user.EmergencyContact.ShouldBe(newEmergencyContact);

        await _permissionService.DidNotReceive()
            .HasAccessAsync(Arg.Any<List<Guid>>(), Arg.Any<AccessRight>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWhenCallerUpdatesOwnProfileViaUsersIdRouteShouldTreatAsSelf()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var user = CreateUser(userId, emergencyContact: "Mom: +380509998877");

        _userRepository.GetForUpdateAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var command = CreateCommand(emergencyContact: null) with
        {
            UserId = userId,
            CurrentUserId = userId,
            TargetCurrentUser = false,
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        user.EmergencyContact.ShouldBeNull();
        await _permissionService.DidNotReceive()
            .HasAccessAsync(Arg.Any<List<Guid>>(), Arg.Any<AccessRight>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Spouse: +380671112233")]
    [InlineData(null)]
    public async Task HandleWhenOtherUserWithViewEmergencyContactPermissionShouldUpdateOrClearEmergencyContact(
        string? newEmergencyContact)
    {
        // Arrange
        var targetUserId = Guid.CreateVersion7();
        var editorUserId = Guid.CreateVersion7();
        var roleIds = new List<Guid> { Guid.CreateVersion7() };
        var user = CreateUser(targetUserId, emergencyContact: "Mom: +380509998877");

        _userRepository.GetForUpdateAsync(targetUserId, Arg.Any<CancellationToken>()).Returns(user);
        _permissionService.HasAccessAsync(roleIds, AccessRight.ViewEmergencyContact, Arg.Any<CancellationToken>())
            .Returns(true);

        var command = CreateCommand(newEmergencyContact) with
        {
            UserId = targetUserId,
            CurrentUserId = editorUserId,
            CurrentUserRoleIds = roleIds,
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        user.FirstName.ShouldBe("UpdatedFirst");
        user.EmergencyContact.ShouldBe(newEmergencyContact);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Injected: +380991112233")]
    public async Task HandleWhenOtherUserWithoutViewEmergencyContactPermissionShouldIgnoreEmergencyContactAndPreserveExistingValue(
        string? incomingEmergencyContact)
    {
        // Arrange
        var targetUserId = Guid.CreateVersion7();
        var editorUserId = Guid.CreateVersion7();
        var roleIds = new List<Guid> { Guid.CreateVersion7() };
        var user = CreateUser(targetUserId, emergencyContact: "Mom: +380509998877");

        _userRepository.GetForUpdateAsync(targetUserId, Arg.Any<CancellationToken>()).Returns(user);
        _permissionService.HasAccessAsync(roleIds, AccessRight.ViewEmergencyContact, Arg.Any<CancellationToken>())
            .Returns(false);

        var command = CreateCommand(incomingEmergencyContact) with
        {
            UserId = targetUserId,
            CurrentUserId = editorUserId,
            CurrentUserRoleIds = roleIds,
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        user.FirstName.ShouldBe("UpdatedFirst");
        user.LastName.ShouldBe("UpdatedLast");
        user.EmergencyContact.ShouldBe("Mom: +380509998877");
    }

    [Fact]
    public async Task HandleWhenOtherUserAndEmergencyContactIsUnchangedShouldSkipPermissionCheck()
    {
        // Arrange
        var targetUserId = Guid.CreateVersion7();
        var editorUserId = Guid.CreateVersion7();
        var user = CreateUser(targetUserId, emergencyContact: null);

        _userRepository.GetForUpdateAsync(targetUserId, Arg.Any<CancellationToken>()).Returns(user);

        var command = CreateCommand(emergencyContact: null) with
        {
            UserId = targetUserId,
            CurrentUserId = editorUserId,
            CurrentUserRoleIds = [Guid.CreateVersion7()],
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        user.FirstName.ShouldBe("UpdatedFirst");
        user.EmergencyContact.ShouldBeNull();
        await _permissionService.DidNotReceive()
            .HasAccessAsync(Arg.Any<List<Guid>>(), Arg.Any<AccessRight>(), Arg.Any<CancellationToken>());
    }

    private static User CreateUser(Guid id, string? emergencyContact) => new()
    {
        Id = id,
        FirstName = "OriginalFirst",
        LastName = "OriginalLast",
        Patronymic = "OriginalMid",
        PhoneNumber = "+380501111111",
        Country = "Ukraine",
        EmergencyContact = emergencyContact,
        PreferredLanguage = "uk",
        Pronouns = "she/her",
        PronounCategory = PronounCategory.Feminine,
    };

    private static UpdateUserProfileCommand CreateCommand(string? emergencyContact) => new(
        FirstName: "UpdatedFirst",
        LastName: "UpdatedLast",
        Patronymic: "UpdatedMid",
        PhoneNumber: "+380502222222",
        Country: "Poland",
        EmergencyContact: emergencyContact,
        PreferredLanguage: "en",
        Pronouns: "they/them",
        PronounCategory: PronounCategory.Neutral);
}
