using System.Text.Json.Nodes;
using Crm.Application.Features.CustomRecords.Commands;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.CustomRecords.Commands;

/// <summary>
/// Unit tests for <see cref="UpdateCustomRecordCommandHandler"/> covering row locking order,
/// partial cell merging, optional clearing, and schema validation failures.
/// </summary>
public class UpdateCustomRecordCommandHandlerTests
{
    private readonly ICustomRecordRepository _recordRepository = Substitute.For<ICustomRecordRepository>();
    private readonly ICustomTableRepository _tableRepository = Substitute.For<ICustomTableRepository>();
    private readonly ILogger<UpdateCustomRecordCommandHandler> _logger = Substitute.For<ILogger<UpdateCustomRecordCommandHandler>>();

    [Fact]
    public async Task HandleWhenPatchingSubsetOfCellsShouldLockTableThenRecordAndPreserveUntouchedCells()
    {
        // Arrange
        var tableId = Guid.CreateVersion7();
        var recordId = Guid.CreateVersion7();
        var phoneField = new CustomField { CustomTableId = tableId, Name = "Phone", IsRequired = true, Order = 0 };
        var noteField = new CustomField { CustomTableId = tableId, Name = "Note", IsRequired = false, Order = 1 };
        string phoneKey = phoneField.Id.ToString();
        string noteKey = noteField.Id.ToString();

        var record = new CustomRecord
        {
            Id = recordId,
            CustomTableId = tableId,
            Position = "a0",
            Data = new JsonObject
            {
                [phoneKey] = "+380991112233",
                [noteKey] = "Old note",
            },
        };

        var callOrder = new List<string>();
        _recordRepository.GetPositionInfoAsync(recordId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callOrder.Add("GetPositionInfo");
                return new CustomRecordPositionInfo(recordId, tableId, "a0");
            });
        _tableRepository.LockForUpdateAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callOrder.Add("LockTable");
                return Task.CompletedTask;
            });
        _recordRepository.GetForUpdateAsync(recordId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callOrder.Add("GetRecordForUpdate");
                return record;
            });
        _tableRepository.GetFieldsByCustomTableIdAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callOrder.Add("GetFields");
                return Task.FromResult<IReadOnlyList<CustomField>>([phoneField, noteField]);
            });

        var handler = new UpdateCustomRecordCommandHandler(_recordRepository, _tableRepository, _logger);

        // Act: patch only Note cell (Phone is omitted)
        await handler.Handle(
            new UpdateCustomRecordCommand(new JsonObject { [noteKey] = "New note" }) { Id = recordId },
            CancellationToken.None);

        // Assert
        callOrder.ShouldBe(["GetPositionInfo", "LockTable", "GetRecordForUpdate", "GetFields"]);
        record.Data[phoneKey]?.GetValue<string>().ShouldBe("+380991112233");
        record.Data[noteKey]?.GetValue<string>().ShouldBe("New note");

        // Act 2: clear optional Note with null while preserving Phone
        await handler.Handle(
            new UpdateCustomRecordCommand(new JsonObject { [noteKey] = null }) { Id = recordId },
            CancellationToken.None);

        record.Data[phoneKey]?.GetValue<string>().ShouldBe("+380991112233");
        record.Data[noteKey].ShouldBeNull();
    }

    [Fact]
    public async Task HandleWhenClearingRequiredFieldOrSendingUnknownKeyShouldThrowInvalidCustomRecordPayloadException()
    {
        // Arrange
        var tableId = Guid.CreateVersion7();
        var recordId = Guid.CreateVersion7();
        var phoneField = new CustomField { CustomTableId = tableId, Name = "Phone", IsRequired = true, Order = 0 };
        string phoneKey = phoneField.Id.ToString();

        var record = new CustomRecord
        {
            Id = recordId,
            CustomTableId = tableId,
            Position = "a0",
            Data = new JsonObject { [phoneKey] = "+380991112233" },
        };

        _recordRepository.GetPositionInfoAsync(recordId, Arg.Any<CancellationToken>())
            .Returns(new CustomRecordPositionInfo(recordId, tableId, "a0"));
        _recordRepository.GetForUpdateAsync(recordId, Arg.Any<CancellationToken>())
            .Returns(record);
        _tableRepository.GetFieldsByCustomTableIdAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CustomField>>([phoneField]));

        var handler = new UpdateCustomRecordCommandHandler(_recordRepository, _tableRepository, _logger);

        // Act & Assert
        await Should.ThrowAsync<InvalidCustomRecordPayloadException>(async () =>
            await handler.Handle(
                new UpdateCustomRecordCommand(new JsonObject { [phoneKey] = null }) { Id = recordId },
                CancellationToken.None));

        await Should.ThrowAsync<InvalidCustomRecordPayloadException>(async () =>
            await handler.Handle(
                new UpdateCustomRecordCommand(new JsonObject { [Guid.CreateVersion7().ToString()] = "ghost" }) { Id = recordId },
                CancellationToken.None));

        await Should.ThrowAsync<InvalidCustomRecordPayloadException>(async () =>
            await handler.Handle(
                new UpdateCustomRecordCommand(new JsonObject { [phoneKey] = 12345 }) { Id = recordId },
                CancellationToken.None));
    }
}
