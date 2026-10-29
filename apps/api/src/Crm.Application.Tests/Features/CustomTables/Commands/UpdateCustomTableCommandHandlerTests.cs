using Crm.Application.Dtos.CustomTables;
using Crm.Application.Dtos.CustomTables.Commands;
using Crm.Application.Features.CustomTables.Commands;
using Crm.Application.Features.CustomTables.Mappings;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.CustomTables.Commands;

/// <summary>
/// Contains unit tests for <see cref="UpdateCustomTableCommandHandler"/>
/// verifying command processing, record synchronization, short-circuiting, and error conditions.
/// </summary>
public class UpdateCustomTableCommandHandlerTests
{
    private readonly ICustomTableRepository _customTableRepository = Substitute.For<ICustomTableRepository>();
    private readonly ICustomRecordRepository _customRecordRepository = Substitute.For<ICustomRecordRepository>();
    private readonly ILogger<UpdateCustomTableCommandHandler> _logger = Substitute.For<ILogger<UpdateCustomTableCommandHandler>>();
    private readonly UpdateCustomTableCommandHandler _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateCustomTableCommandHandlerTests"/> class.
    /// Configures Mapster mapping definitions and initializes the handler under test.
    /// </summary>
    public UpdateCustomTableCommandHandlerTests()
    {
        new CustomTableMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
        _handler = new UpdateCustomTableCommandHandler(_customTableRepository, _customRecordRepository, _logger);
    }

    /// <summary>
    /// Verifies that when the custom table contains no existing records, metadata and fields are updated,
    /// but record payload synchronization is bypassed (short-circuited).
    /// </summary>
    [Fact]
    public async Task HandleWhenTableHasNoRecordsShouldUpdateSchemaAndBypassRecordSync()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var existingFieldId = Guid.NewGuid();
        var customTable = new CustomTable
        {
            Id = tableId,
            Name = "Initial Name",
            Description = "Initial Description",
            Fields =
            [
                new CustomField { Id = existingFieldId, Key = "field_one", Name = "Field One", DataType = FieldDataType.Text },
            ],
        };

        _customTableRepository.GetForUpdateWithFieldsAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(customTable);

        _customRecordRepository.AnyAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(false);

        var command = new UpdateCustomTableCommand(
            tableId,
            "Updated Name",
            "Updated Description",
            [
                new UpdateCustomFieldDto(existingFieldId, "Field One Renamed", "field_one", FieldDataType.Text, false, 0),
                new UpdateCustomFieldDto(null, "Field Two", "field_two", FieldDataType.Number, true, 1),
            ]);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        customTable.Name.ShouldBe("Updated Name");
        customTable.Description.ShouldBe("Updated Description");
        customTable.Fields.Count.ShouldBe(2);
        customTable.Fields.ShouldContain(f => f.Key == "field_two" && f.IsRequired);

        await _customRecordRepository.DidNotReceive()
            .SyncRecordSchemaAsync(Arg.Any<Guid>(), Arg.Any<SchemaDiffResult>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that when the custom table contains existing records, record schema synchronization
    /// is invoked with the calculated schema diff.
    /// </summary>
    [Fact]
    public async Task HandleWhenTableHasRecordsShouldInvokeSyncRecordSchema()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var customTable = new CustomTable
        {
            Id = tableId,
            Name = "Active Table",
            Fields =
            [
                new CustomField { Id = fieldId, Key = "status", Name = "Status", DataType = FieldDataType.Text, IsRequired = false },
            ],
        };

        _customTableRepository.GetForUpdateWithFieldsAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(customTable);

        _customRecordRepository.AnyAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(true);

        var command = new UpdateCustomTableCommand(
            tableId,
            "Active Table",
            null,
            [
                new UpdateCustomFieldDto(fieldId, "Status", "status", FieldDataType.Text, true, 0),
            ]);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _customRecordRepository.Received(1)
            .SyncRecordSchemaAsync(
                tableId,
                Arg.Is<SchemaDiffResult>(d => d.FieldsNeedingBackfill.Count == 1 && d.FieldsNeedingBackfill[0].Key == "status"),
                Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that when the custom table cannot be found, an <see cref="EntityNotFoundException{CustomTable}"/> is thrown.
    /// </summary>
    [Fact]
    public async Task HandleWhenTableNotFoundShouldThrowEntityNotFoundException()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        _customTableRepository.GetForUpdateWithFieldsAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<CustomTable>(new EntityNotFoundException<CustomTable>(tableId)));

        var command = new UpdateCustomTableCommand(tableId, "Name", null, []);

        // Act & Assert
        await Should.ThrowAsync<EntityNotFoundException<CustomTable>>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that modifying a field data type when existing records are present throws a <see cref="FieldDataTypeChangeNotAllowedException"/>.
    /// </summary>
    [Fact]
    public async Task HandleWhenDataTypeChangedWithExistingRecordsShouldThrowException()
    {
        // Arrange
        var tableId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var customTable = new CustomTable
        {
            Id = tableId,
            Name = "Table With Records",
            Fields =
            [
                new CustomField { Id = fieldId, Key = "quantity", Name = "Quantity", DataType = FieldDataType.Number },
            ],
        };

        _customTableRepository.GetForUpdateWithFieldsAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(customTable);

        _customRecordRepository.AnyAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(true);

        var command = new UpdateCustomTableCommand(
            tableId,
            "Table With Records",
            null,
            [
                new UpdateCustomFieldDto(fieldId, "Quantity", "quantity", FieldDataType.Text, false, 0),
            ]);

        // Act & Assert
        await Should.ThrowAsync<FieldDataTypeChangeNotAllowedException>(() =>
            _handler.Handle(command, CancellationToken.None));

        await _customRecordRepository.DidNotReceive()
            .SyncRecordSchemaAsync(Arg.Any<Guid>(), Arg.Any<SchemaDiffResult>(), Arg.Any<CancellationToken>());
    }
}
