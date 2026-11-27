using Crm.Application.Dtos.CustomTables;
using Crm.Application.Features.CustomTables.Commands;
using Crm.Application.Features.CustomTables.Mappings;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.CustomTables.Commands;

/// <summary>
/// Contains unit tests for <see cref="UpdateCustomTableCommandHandler"/>
/// verifying command processing, field ID preservation, record synchronization, short-circuiting, and error conditions.
/// </summary>
public class UpdateCustomTableCommandHandlerTests
{
    private readonly ICustomTableRepository _customTableRepository = Substitute.For<ICustomTableRepository>();
    private readonly ICustomRecordRepository _customRecordRepository = Substitute.For<ICustomRecordRepository>();
    private readonly ILogger<UpdateCustomTableCommandHandler> _logger = Substitute.For<ILogger<UpdateCustomTableCommandHandler>>();
    private readonly UpdateCustomTableCommandHandler _handler;

    public UpdateCustomTableCommandHandlerTests()
    {
        new CustomTableMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
        _handler = new UpdateCustomTableCommandHandler(_customTableRepository, _customRecordRepository, _logger);
    }

    [Fact]
    public async Task HandleWhenTableHasNoRecordsShouldUpdateSchemaStageChangesAndBypassRecordSync()
    {
        // Arrange
        var tableId = Guid.CreateVersion7();
        var existingFieldId = Guid.CreateVersion7();
        var customTable = new CustomTable
        {
            Id = tableId,
            Name = "Initial Name",
            Description = "Initial Description",
            Fields =
            [
                new CustomField { Id = existingFieldId, Name = "Field One" },
            ],
        };

        _customTableRepository.GetForUpdateWithFieldsAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(customTable);

        _customRecordRepository.AnyAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(false);

        var command = new UpdateCustomTableCommand(
            "Updated Name",
            "Updated Description",
            [
                new UpdateCustomFieldDto(existingFieldId, "Field One Renamed", false, 0),
                new UpdateCustomFieldDto(null, "Field Two", true, 1),
            ])
        {
            Id = tableId,
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _customTableRepository.Received(1).LockForUpdateAsync(tableId, Arg.Any<CancellationToken>());
        await _customTableRepository.Received(1).ApplyFieldSchemaChangesAsync(
            tableId,
            Arg.Is<IEnumerable<CustomField>>(added => added.Count() == 1),
            Arg.Is<IEnumerable<CustomField>>(removed => !removed.Any()),
            Arg.Any<CancellationToken>());

        customTable.Name.ShouldBe("Updated Name");
        customTable.Description.ShouldBe("Updated Description");
        customTable.Fields.Count.ShouldBe(2);
        customTable.Fields.Single(f => f.Id == existingFieldId).Name.ShouldBe("Field One Renamed");

        var addedField = customTable.Fields.Single(f => f.Name == "Field Two");
        addedField.IsRequired.ShouldBeTrue();
        addedField.Id.ShouldNotBe(Guid.Empty);

        await _customRecordRepository.DidNotReceive()
            .SyncRecordSchemaAsync(Arg.Any<Guid>(), Arg.Any<SchemaSyncPlan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWhenOnlyFieldNameChangesShouldSkipRecordCheckAndJsonbSync()
    {
        // Arrange
        var tableId = Guid.CreateVersion7();
        var fieldId = Guid.CreateVersion7();
        var customTable = new CustomTable
        {
            Id = tableId,
            Name = "Active Table",
            Fields =
            [
                new CustomField { Id = fieldId, Name = "Old Name", IsRequired = false },
            ],
        };

        _customTableRepository.GetForUpdateWithFieldsAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(customTable);

        var command = new UpdateCustomTableCommand(
            "Active Table",
            null,
            [
                new UpdateCustomFieldDto(fieldId, "New Name", false, 0),
            ])
        {
            Id = tableId,
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _customRecordRepository.DidNotReceive().AnyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _customRecordRepository.DidNotReceive()
            .SyncRecordSchemaAsync(Arg.Any<Guid>(), Arg.Any<SchemaSyncPlan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWhenTableHasRecordsAndFieldBecomesRequiredShouldInvokeSyncRecordSchemaWithFieldId()
    {
        // Arrange
        var tableId = Guid.CreateVersion7();
        var fieldId = Guid.CreateVersion7();
        var customTable = new CustomTable
        {
            Id = tableId,
            Name = "Active Table",
            Fields =
            [
                new CustomField { Id = fieldId, Name = "Status", IsRequired = false },
            ],
        };

        _customTableRepository.GetForUpdateWithFieldsAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(customTable);

        _customRecordRepository.AnyAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(true);

        var command = new UpdateCustomTableCommand(
            "Active Table",
            null,
            [
                new UpdateCustomFieldDto(fieldId, "Status", true, 0),
            ])
        {
            Id = tableId,
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _customTableRepository.Received(1).LockForUpdateAsync(tableId, Arg.Any<CancellationToken>());
        await _customRecordRepository.Received(1)
            .SyncRecordSchemaAsync(
                tableId,
                Arg.Is<SchemaSyncPlan>(p => p.KeysToBackfill.Length == 1 && p.KeysToBackfill[0] == fieldId.ToString()),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWhenTableNotFoundShouldThrowEntityNotFoundException()
    {
        // Arrange
        var tableId = Guid.CreateVersion7();
        _customTableRepository.GetForUpdateWithFieldsAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<CustomTable>(new EntityNotFoundException<CustomTable>(tableId)));

        var command = new UpdateCustomTableCommand("Name", null, [])
        {
            Id = tableId,
        };

        // Act & Assert
        await Should.ThrowAsync<EntityNotFoundException<CustomTable>>(() =>
            _handler.Handle(command, CancellationToken.None).AsTask());

        await _customTableRepository.Received(1).LockForUpdateAsync(tableId, Arg.Any<CancellationToken>());
    }
}
