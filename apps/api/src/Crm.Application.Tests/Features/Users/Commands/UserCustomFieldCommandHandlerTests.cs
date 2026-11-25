using System.Text.Json.Nodes;
using Crm.Application.Dtos.Users;
using Crm.Application.Features.Users.Commands;
using Crm.Application.Features.Users.Mappings;
using Crm.Application.Features.Users.Queries;
using Crm.Application.Features.Users.Validators;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.Users.Commands;

/// <summary>
/// Unit tests covering global user custom field schema synchronization, per-user JSONB payload updates,
/// schema definition queries, and shared validators.
/// </summary>
public class UserCustomFieldCommandHandlerTests
{
    private static readonly string[] AgeOnlyKeys = ["age"];

    private readonly IUserCustomFieldRepository _customFieldRepository = Substitute.For<IUserCustomFieldRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPermissionService _permissionService = Substitute.For<IPermissionService>();

    static UserCustomFieldCommandHandlerTests()
    {
        new UserMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
    }

    [Fact]
    public async Task SyncUserCustomFieldsShouldAddRenameBackfillRemoveAndPatchUserJsonbPayloads()
    {
        // Arrange
        var cityFieldId = Guid.CreateVersion7();
        var obsoleteFieldId = Guid.CreateVersion7();

        var existingFields = new List<UserCustomField>
        {
            new() { Id = cityFieldId, Name = "City", Key = "city", DataType = FieldDataType.Text, IsRequired = false, IsPublic = true, Order = 0 },
            new() { Id = obsoleteFieldId, Name = "Old Note", Key = "old_note", DataType = FieldDataType.Text, IsRequired = false, IsPublic = false, Order = 1 },
        };

        _customFieldRepository.GetAllForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns(existingFields);

        List<UserCustomField>? addedCapture = null;
        List<UserCustomField>? removedCapture = null;
        _customFieldRepository.ApplySchemaChangesAsync(
                Arg.Do<IEnumerable<UserCustomField>>(a => addedCapture = [.. a]),
                Arg.Do<IEnumerable<UserCustomField>>(r => removedCapture = [.. r]),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        SchemaDiffResult? capturedDiff = null;
        _userRepository.SyncCustomFieldsSchemaAsync(
                Arg.Do<SchemaDiffResult>(d => capturedDiff = d),
                Arg.Any<CancellationToken>())
            .Returns(3);

        var handler = new SyncUserCustomFieldsCommandHandler(
            _customFieldRepository,
            _userRepository,
            Substitute.For<ILogger<SyncUserCustomFieldsCommandHandler>>());

        var command = new SyncUserCustomFieldsCommand(
        [
            new UpdateUserCustomFieldDto(cityFieldId, "Location", "location", FieldDataType.Text, IsRequired: true, IsPublic: true, Order: 0),
            new UpdateUserCustomFieldDto(null, "Experience Years", "experience_years", FieldDataType.Number, IsRequired: true, IsPublic: false, Order: 1),
        ]);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        addedCapture.ShouldNotBeNull();
        addedCapture.ShouldHaveSingleItem().Key.ShouldBe("experience_years");

        removedCapture.ShouldNotBeNull();
        removedCapture.ShouldHaveSingleItem().Id.ShouldBe(obsoleteFieldId);

        capturedDiff.ShouldNotBeNull();
        capturedDiff.FieldsToRemove.ShouldHaveSingleItem().Key.ShouldBe("old_note");
        capturedDiff.FieldsToAdd.ShouldHaveSingleItem().Key.ShouldBe("experience_years");
        capturedDiff.KeyRenames.ShouldHaveSingleItem().OldKey.ShouldBe("city");
        capturedDiff.KeyRenames.Single().NewKey.ShouldBe("location");
        capturedDiff.FieldsNeedingBackfill.ShouldHaveSingleItem().Key.ShouldBe("location");

        await _userRepository.DidNotReceive()
            .GetKeysWithNonNullCustomFieldValuesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        await _customFieldRepository.Received(1).ResolveFieldKeyCollisionsAsync(Arg.Any<CancellationToken>());
        await _userRepository.Received(1).SyncCustomFieldsSchemaAsync(Arg.Any<SchemaDiffResult>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncUserCustomFieldsWhenDataTypeChangesWithExistingNonNullValuesShouldThrowFieldDataTypeChangeNotAllowedException()
    {
        // Arrange
        var ageFieldId = Guid.CreateVersion7();
        var existingFields = new List<UserCustomField>
        {
            new() { Id = ageFieldId, Name = "Age", Key = "age", DataType = FieldDataType.Number, IsRequired = false, IsPublic = true, Order = 0 },
        };

        _customFieldRepository.GetAllForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns(existingFields);
        _userRepository.GetKeysWithNonNullCustomFieldValuesAsync(
                Arg.Is<IEnumerable<string>>(keys => keys.SequenceEqual(AgeOnlyKeys)),
                Arg.Any<CancellationToken>())
            .Returns(new HashSet<string>(StringComparer.Ordinal) { "age" });

        var handler = new SyncUserCustomFieldsCommandHandler(
            _customFieldRepository,
            _userRepository,
            Substitute.For<ILogger<SyncUserCustomFieldsCommandHandler>>());

        var command = new SyncUserCustomFieldsCommand(
        [
            new UpdateUserCustomFieldDto(ageFieldId, "Age", "age", FieldDataType.Date, IsRequired: false, IsPublic: true, Order: 0),
        ]);

        // Act & Assert
        await Should.ThrowAsync<FieldDataTypeChangeNotAllowedException>(
            async () => await handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task SyncUserCustomFieldsWhenDataTypeChangesAndNoUsersHaveValuesShouldAllowChange()
    {
        // Arrange
        var fieldId = Guid.CreateVersion7();
        var existingFields = new List<UserCustomField>
        {
            new() { Id = fieldId, Name = "Metric", Key = "metric", DataType = FieldDataType.Text, IsRequired = false, IsPublic = true, Order = 0 },
        };

        _customFieldRepository.GetAllForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns(existingFields);
        _userRepository.GetKeysWithNonNullCustomFieldValuesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<string>(StringComparer.Ordinal));

        var handler = new SyncUserCustomFieldsCommandHandler(
            _customFieldRepository,
            _userRepository,
            Substitute.For<ILogger<SyncUserCustomFieldsCommandHandler>>());

        var command = new SyncUserCustomFieldsCommand(
        [
            new UpdateUserCustomFieldDto(fieldId, "Metric", "metric", FieldDataType.Number, IsRequired: false, IsPublic: true, Order: 0),
        ]);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        existingFields.Single().DataType.ShouldBe(FieldDataType.Number);
    }

    [Fact]
    public async Task SyncUserCustomFieldsWhenEmptyShouldAllowRemovingAllGlobalFields()
    {
        // Arrange
        var fieldId = Guid.CreateVersion7();
        var existingFields = new List<UserCustomField>
        {
            new() { Id = fieldId, Name = "City", Key = "city", DataType = FieldDataType.Text, IsRequired = false, IsPublic = true, Order = 0 },
        };

        _customFieldRepository.GetAllForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns(existingFields);

        var handler = new SyncUserCustomFieldsCommandHandler(
            _customFieldRepository,
            _userRepository,
            Substitute.For<ILogger<SyncUserCustomFieldsCommandHandler>>());

        // Act
        await handler.Handle(new SyncUserCustomFieldsCommand([]), CancellationToken.None);

        // Assert
        existingFields.ShouldBeEmpty();
        await _userRepository.Received(1).SyncCustomFieldsSchemaAsync(
            Arg.Is<SchemaDiffResult>(d => d.FieldsToRemove.Count == 1 && d.FieldsToRemove[0].Key == "city"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateUserCustomFieldsWhenSelfShouldValidateAndBackfillAllFieldsIncludingPrivate()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var user = new User { Id = userId, CustomFields = [] };
        var schema = new List<UserCustomField>
        {
            new() { Id = Guid.CreateVersion7(), Name = "City", Key = "city", DataType = FieldDataType.Text, IsRequired = true, IsPublic = true, Order = 0 },
            new() { Id = Guid.CreateVersion7(), Name = "Experience", Key = "experience", DataType = FieldDataType.Number, IsRequired = false, IsPublic = false, Order = 1 },
        };

        _userRepository.GetForUpdateAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _customFieldRepository.GetAllOrderedAsync(Arg.Any<CancellationToken>()).Returns(schema);

        var handler = new UpdateUserCustomFieldsCommandHandler(
            _userRepository,
            _customFieldRepository,
            _permissionService,
            Substitute.For<ILogger<UpdateUserCustomFieldsCommandHandler>>());

        var command = new UpdateUserCustomFieldsCommand(new JsonObject { ["city"] = "Kyiv" })
        {
            UserId = userId,
            CurrentUserId = userId,
            TargetCurrentUser = true,
        };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        user.CustomFields.Select(kvp => kvp.Key).ShouldBe(["city", "experience"]);
        user.CustomFields["city"]?.GetValue<string>().ShouldBe("Kyiv");
        user.CustomFields["experience"].ShouldBeNull();
        await _permissionService.DidNotReceive()
            .HasAccessAsync(Arg.Any<List<Guid>>(), Arg.Any<AccessRight>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateUserCustomFieldsWhenOtherUserWithoutViewCustomFieldsShouldPreservePrivateFieldsAndRejectPrivateEdits()
    {
        // Arrange
        var targetUserId = Guid.CreateVersion7();
        var editorUserId = Guid.CreateVersion7();
        var editorRoles = new List<Guid> { Guid.CreateVersion7() };
        var user = new User
        {
            Id = targetUserId,
            CustomFields = new JsonObject
            {
                ["city"] = "Kyiv",
                ["salary"] = 5000,
            },
        };
        var schema = new List<UserCustomField>
        {
            new() { Id = Guid.CreateVersion7(), Name = "City", Key = "city", DataType = FieldDataType.Text, IsRequired = true, IsPublic = true, Order = 0 },
            new() { Id = Guid.CreateVersion7(), Name = "Salary", Key = "salary", DataType = FieldDataType.Number, IsRequired = true, IsPublic = false, Order = 1 },
        };

        _userRepository.GetForUpdateAsync(targetUserId, Arg.Any<CancellationToken>()).Returns(user);
        _customFieldRepository.GetAllOrderedAsync(Arg.Any<CancellationToken>()).Returns(schema);
        _permissionService.HasAccessAsync(editorRoles, AccessRight.ViewCustomFields, Arg.Any<CancellationToken>()).Returns(false);

        var handler = new UpdateUserCustomFieldsCommandHandler(
            _userRepository,
            _customFieldRepository,
            _permissionService,
            Substitute.For<ILogger<UpdateUserCustomFieldsCommandHandler>>());

        // Act 1: updating only public field preserves existing required private field value
        await handler.Handle(
            new UpdateUserCustomFieldsCommand(new JsonObject { ["city"] = "Lviv" })
            {
                UserId = targetUserId,
                CurrentUserId = editorUserId,
                CurrentUserRoleIds = editorRoles,
            },
            CancellationToken.None);

        // Assert 1
        user.CustomFields.Select(kvp => kvp.Key).ShouldBe(["city", "salary"]);
        user.CustomFields["city"]?.GetValue<string>().ShouldBe("Lviv");
        user.CustomFields["salary"]?.GetValue<int>().ShouldBe(5000);

        // Act & Assert 2: attempting to send a private field key is rejected
        await Should.ThrowAsync<InvalidCustomRecordPayloadException>(async () =>
            await handler.Handle(
                new UpdateUserCustomFieldsCommand(new JsonObject { ["city"] = "Lviv", ["salary"] = 99999 })
                {
                    UserId = targetUserId,
                    CurrentUserId = editorUserId,
                    CurrentUserRoleIds = editorRoles,
                },
                CancellationToken.None));
    }

    [Fact]
    public async Task GetUserCustomFieldsQueryHandlerShouldFilterPrivateFieldsUnlessAuthorizedOrIncludePrivate()
    {
        // Arrange
        var roleIds = new List<Guid> { Guid.NewGuid() };
        var schema = new List<UserCustomField>
        {
            new() { Id = Guid.CreateVersion7(), Name = "City", Key = "city", DataType = FieldDataType.Text, IsRequired = false, IsPublic = true, Order = 0 },
            new() { Id = Guid.CreateVersion7(), Name = "Salary", Key = "salary", DataType = FieldDataType.Number, IsRequired = false, IsPublic = false, Order = 1 },
        };

        _customFieldRepository.GetAllOrderedAsync(Arg.Any<CancellationToken>()).Returns(schema);
        _permissionService.HasAccessAsync(roleIds, AccessRight.ViewCustomFields, Arg.Any<CancellationToken>()).Returns(false);

        var handler = new GetUserCustomFieldsQueryHandler(
            _customFieldRepository,
            _permissionService,
            Substitute.For<ILogger<GetUserCustomFieldsQueryHandler>>());

        // Act: regular table columns query
        var tableColumns = await handler.Handle(new GetUserCustomFieldsQuery { CurrentUserRoleIds = roleIds }, CancellationToken.None);

        // Act: self-profile form columns query (IncludePrivate = true)
        var selfColumns = await handler.Handle(new GetUserCustomFieldsQuery { IncludePrivate = true, CurrentUserRoleIds = roleIds }, CancellationToken.None);

        // Assert
        tableColumns.ShouldHaveSingleItem().Key.ShouldBe("city");
        selfColumns.Count.ShouldBe(2);
        selfColumns[0].Name.ShouldBe("City");
        selfColumns[1].Key.ShouldBe("salary");
    }

    [Fact]
    public void ValidatorsShouldReuseFieldListAndJsonPayloadRules()
    {
        var syncValidator = new SyncUserCustomFieldsCommandValidator();

        // Empty list is valid (removes all columns)
        syncValidator.Validate(new SyncUserCustomFieldsCommand([])).IsValid.ShouldBeTrue();

        // Reserved user key rejected
        syncValidator.Validate(new SyncUserCustomFieldsCommand(
        [
            new UpdateUserCustomFieldDto(null, "Email", UserConstants.Email.ToLowerInvariant(), FieldDataType.Email, false, true, 0),
        ])).IsValid.ShouldBeFalse();

        // Non-snake_case key rejected
        syncValidator.Validate(new SyncUserCustomFieldsCommand(
        [
            new UpdateUserCustomFieldDto(null, "City", "CityName", FieldDataType.Text, false, true, 0),
        ])).IsValid.ShouldBeFalse();

        // Non-sequential order rejected
        syncValidator.Validate(new SyncUserCustomFieldsCommand(
        [
            new UpdateUserCustomFieldDto(null, "City", "city", FieldDataType.Text, false, true, 1),
        ])).IsValid.ShouldBeFalse();

        // Valid schema accepted
        syncValidator.Validate(new SyncUserCustomFieldsCommand(
        [
            new UpdateUserCustomFieldDto(null, "Місто", "city", FieldDataType.Text, false, true, 0),
            new UpdateUserCustomFieldDto(null, "Досвід", "experience_years", FieldDataType.Number, true, false, 1),
        ])).IsValid.ShouldBeTrue();

        var updateValidator = new UpdateUserCustomFieldsCommandValidator();
        updateValidator.Validate(new UpdateUserCustomFieldsCommand(new JsonObject { ["city"] = "Kyiv" }) { UserId = Guid.Empty }).IsValid.ShouldBeFalse();
        updateValidator.Validate(new UpdateUserCustomFieldsCommand(new JsonObject { ["city"] = "bad\0val" }) { UserId = Guid.NewGuid() }).IsValid.ShouldBeFalse();
        updateValidator.Validate(new UpdateUserCustomFieldsCommand(new JsonObject { ["city"] = "Kyiv" }) { UserId = Guid.NewGuid() }).IsValid.ShouldBeTrue();
    }
}
