using System.Text.Json.Nodes;
using Crm.Application.Dtos.Users;
using Crm.Application.Features.Users.Commands;
using Crm.Application.Features.Users.Mappings;
using Crm.Application.Features.Users.Queries;
using Crm.Application.Features.Users.Validators;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
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
/// Unit tests covering global user custom field schema synchronization keyed by field <see cref="Domain.Entities.Base.BaseEntity.Id"/>,
/// per-user JSONB payload updates, schema definition queries, and shared validators.
/// </summary>
public class UserCustomFieldCommandHandlerTests
{
    private readonly IUserCustomFieldRepository _customFieldRepository = Substitute.For<IUserCustomFieldRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPermissionService _permissionService = Substitute.For<IPermissionService>();

    static UserCustomFieldCommandHandlerTests()
    {
        new UserMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
    }

    [Fact]
    public async Task SyncUserCustomFieldsShouldPreserveExistingIdsGenerateNewIdsAndPatchUserJsonbPayloads()
    {
        // Arrange
        var cityFieldId = Guid.CreateVersion7();
        var obsoleteFieldId = Guid.CreateVersion7();

        var existingFields = new List<UserCustomField>
        {
            new() { Id = cityFieldId, Name = "City", IsRequired = false, IsPublic = true, Order = 0 },
            new() { Id = obsoleteFieldId, Name = "Old Note", IsRequired = false, IsPublic = false, Order = 1 },
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

        SchemaSyncPlan? capturedPlan = null;
        _userRepository.SyncCustomFieldsSchemaAsync(
                Arg.Do<SchemaSyncPlan>(p => capturedPlan = p),
                Arg.Any<CancellationToken>())
            .Returns(3);

        var handler = new SyncUserCustomFieldsCommandHandler(
            _customFieldRepository,
            _userRepository,
            Substitute.For<ILogger<SyncUserCustomFieldsCommandHandler>>());

        var command = new SyncUserCustomFieldsCommand(
        [
            new UpdateUserCustomFieldDto(cityFieldId, "Location", IsRequired: true, IsPublic: true, Order: 0),
            new UpdateUserCustomFieldDto(null, "Experience Years", IsRequired: true, IsPublic: false, Order: 1),
        ]);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        addedCapture.ShouldNotBeNull();
        var addedField = addedCapture.ShouldHaveSingleItem();
        addedField.Name.ShouldBe("Experience Years");
        addedField.Id.ShouldNotBe(Guid.Empty);

        removedCapture.ShouldNotBeNull();
        removedCapture.ShouldHaveSingleItem().Id.ShouldBe(obsoleteFieldId);

        existingFields.Single(f => f.Id == cityFieldId).Name.ShouldBe("Location");

        capturedPlan.ShouldNotBeNull();
        capturedPlan.KeysToDrop.ShouldBe([obsoleteFieldId.ToString()]);
        capturedPlan.KeysToBackfill.ShouldBe([cityFieldId.ToString(), addedField.Id.ToString()], ignoreOrder: true);
        capturedPlan.RequiresJsonbSync.ShouldBeTrue();

        await _userRepository.Received(1).SyncCustomFieldsSchemaAsync(Arg.Any<SchemaSyncPlan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncUserCustomFieldsWhenEmptyShouldAllowRemovingAllGlobalFields()
    {
        // Arrange
        var fieldId = Guid.CreateVersion7();
        var existingFields = new List<UserCustomField>
        {
            new() { Id = fieldId, Name = "City", IsRequired = false, IsPublic = true, Order = 0 },
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
            Arg.Is<SchemaSyncPlan>(p => p.KeysToDrop.Length == 1 && p.KeysToDrop[0] == fieldId.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateUserCustomFieldsWhenSelfShouldValidateAndBackfillAllFieldsIncludingPrivate()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var user = new User { Id = userId, CustomFields = [] };
        var cityField = new UserCustomField { Name = "City", IsRequired = true, IsPublic = true, Order = 0 };
        var expField = new UserCustomField { Name = "Experience", IsRequired = false, IsPublic = false, Order = 1 };
        var schema = new List<UserCustomField> { cityField, expField };

        _userRepository.GetForUpdateAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _customFieldRepository.GetAllOrderedAsync(Arg.Any<CancellationToken>()).Returns(schema);

        var handler = new UpdateUserCustomFieldsCommandHandler(
            _userRepository,
            _customFieldRepository,
            _permissionService,
            Substitute.For<ILogger<UpdateUserCustomFieldsCommandHandler>>());

        var command = new UpdateUserCustomFieldsCommand(new JsonObject { [cityField.Id.ToString()] = "Kyiv" })
        {
            UserId = userId,
            CurrentUserId = userId,
            TargetCurrentUser = true,
        };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        user.CustomFields.Select(kvp => kvp.Key).ShouldBe([cityField.Id.ToString(), expField.Id.ToString()]);
        user.CustomFields[cityField.Id.ToString()]?.GetValue<string>().ShouldBe("Kyiv");
        user.CustomFields[expField.Id.ToString()].ShouldBeNull();
        await _permissionService.DidNotReceive()
            .HasAccessAsync(Arg.Any<List<Guid>>(), Arg.Any<AccessRight>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateUserCustomFieldsWhenNonStringValueProvidedShouldThrowInvalidCustomRecordPayloadException()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var user = new User { Id = userId, CustomFields = [] };
        var expField = new UserCustomField { Name = "Experience", IsRequired = true, IsPublic = true, Order = 0 };
        var schema = new List<UserCustomField> { expField };

        _userRepository.GetForUpdateAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _customFieldRepository.GetAllOrderedAsync(Arg.Any<CancellationToken>()).Returns(schema);

        var handler = new UpdateUserCustomFieldsCommandHandler(
            _userRepository,
            _customFieldRepository,
            _permissionService,
            Substitute.For<ILogger<UpdateUserCustomFieldsCommandHandler>>());

        var command = new UpdateUserCustomFieldsCommand(new JsonObject { [expField.Id.ToString()] = 10 })
        {
            UserId = userId,
            CurrentUserId = userId,
            TargetCurrentUser = true,
        };

        // Act & Assert
        await Should.ThrowAsync<InvalidCustomRecordPayloadException>(
            async () => await handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateUserCustomFieldsWhenOtherUserWithoutViewCustomFieldsShouldPreservePrivateFieldsAndRejectPrivateEdits()
    {
        // Arrange
        var targetUserId = Guid.CreateVersion7();
        var editorUserId = Guid.CreateVersion7();
        var editorRoles = new List<Guid> { Guid.CreateVersion7() };
        var cityField = new UserCustomField { Name = "City", IsRequired = true, IsPublic = true, Order = 0 };
        var salaryField = new UserCustomField { Name = "Salary", IsRequired = true, IsPublic = false, Order = 1 };
        string cityKey = cityField.Id.ToString();
        string salaryKey = salaryField.Id.ToString();

        var user = new User
        {
            Id = targetUserId,
            CustomFields = new JsonObject
            {
                [cityKey] = "Kyiv",
                [salaryKey] = "5000",
            },
        };
        var schema = new List<UserCustomField> { cityField, salaryField };

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
            new UpdateUserCustomFieldsCommand(new JsonObject { [cityKey] = "Lviv" })
            {
                UserId = targetUserId,
                CurrentUserId = editorUserId,
                CurrentUserRoleIds = editorRoles,
            },
            CancellationToken.None);

        // Assert 1
        user.CustomFields.Select(kvp => kvp.Key).ShouldBe([cityKey, salaryKey]);
        user.CustomFields[cityKey]?.GetValue<string>().ShouldBe("Lviv");
        user.CustomFields[salaryKey]?.GetValue<string>().ShouldBe("5000");

        // Act & Assert 2: attempting to send a private field ID is rejected
        await Should.ThrowAsync<InvalidCustomRecordPayloadException>(async () =>
            await handler.Handle(
                new UpdateUserCustomFieldsCommand(new JsonObject { [cityKey] = "Lviv", [salaryKey] = "99999" })
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
        var cityField = new UserCustomField { Name = "City", IsRequired = false, IsPublic = true, Order = 0 };
        var salaryField = new UserCustomField { Name = "Salary", IsRequired = false, IsPublic = false, Order = 1 };
        var schema = new List<UserCustomField> { cityField, salaryField };

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
        tableColumns.ShouldHaveSingleItem().Id.ShouldBe(cityField.Id);
        selfColumns.Count.ShouldBe(2);
        selfColumns[0].Name.ShouldBe("City");
        selfColumns[1].Id.ShouldBe(salaryField.Id);
    }

    [Fact]
    public void ValidatorsShouldReuseFieldListAndJsonPayloadRules()
    {
        var syncValidator = new SyncUserCustomFieldsCommandValidator();

        // Empty list is valid (removes all columns)
        syncValidator.Validate(new SyncUserCustomFieldsCommand([])).IsValid.ShouldBeTrue();

        // Blank name rejected
        syncValidator.Validate(new SyncUserCustomFieldsCommand(
        [
            new UpdateUserCustomFieldDto(null, "   ", false, true, 0),
        ])).IsValid.ShouldBeFalse();

        // Duplicate IDs rejected
        var dupId = Guid.CreateVersion7();
        syncValidator.Validate(new SyncUserCustomFieldsCommand(
        [
            new UpdateUserCustomFieldDto(dupId, "City", false, true, 0),
            new UpdateUserCustomFieldDto(dupId, "Country", false, true, 1),
        ])).IsValid.ShouldBeFalse();

        // Non-sequential order rejected
        syncValidator.Validate(new SyncUserCustomFieldsCommand(
        [
            new UpdateUserCustomFieldDto(null, "City", false, true, 1),
        ])).IsValid.ShouldBeFalse();

        // Valid schema accepted
        syncValidator.Validate(new SyncUserCustomFieldsCommand(
        [
            new UpdateUserCustomFieldDto(null, "Місто", false, true, 0),
            new UpdateUserCustomFieldDto(null, "Досвід", true, false, 1),
        ])).IsValid.ShouldBeTrue();

        var fieldIdKey = Guid.CreateVersion7().ToString();
        var updateValidator = new UpdateUserCustomFieldsCommandValidator();
        updateValidator.Validate(new UpdateUserCustomFieldsCommand(new JsonObject { [fieldIdKey] = "Kyiv" }) { UserId = Guid.Empty }).IsValid.ShouldBeFalse();
        updateValidator.Validate(new UpdateUserCustomFieldsCommand(new JsonObject { [fieldIdKey] = "bad\0val" }) { UserId = Guid.NewGuid() }).IsValid.ShouldBeFalse();
        updateValidator.Validate(new UpdateUserCustomFieldsCommand(new JsonObject { [fieldIdKey] = "Kyiv" }) { UserId = Guid.NewGuid() }).IsValid.ShouldBeTrue();
    }
}
