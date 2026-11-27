using System.Text.Json.Nodes;
using Crm.Application.Common.Behaviors;
using Crm.Application.Dtos.CustomRecords;
using Crm.Application.Dtos.CustomTables;
using Crm.Application.Features.CustomRecords.Commands;
using Crm.Application.Features.CustomRecords.Queries;
using Crm.Application.Features.CustomRecords.Services;
using Crm.Application.Features.CustomRecords.Validators;
using Crm.Application.Features.CustomTables.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Features.CustomRecords.Services;

/// <summary>
/// Unit tests for <see cref="CustomRecordPositionService"/>, custom record command handlers and validators,
/// and <see cref="ValidationBehavior{TRequest, TResponse}"/> automatic <c>IHasEntityId</c> validation.
/// </summary>
public class CustomRecordPositionServiceTests
{
    private readonly ICustomTableRepository _tableRepository = Substitute.For<ICustomTableRepository>();
    private readonly ICustomRecordRepository _recordRepository = Substitute.For<ICustomRecordRepository>();
    private readonly CustomRecordPositionService _service;

    public CustomRecordPositionServiceTests()
    {
        _service = new CustomRecordPositionService(_tableRepository, _recordRepository);
    }

    [Fact]
    public async Task ComputeInsertPositionWhenEmptyTableShouldReturnInitialRank()
    {
        var tableId = Guid.CreateVersion7();

        _recordRepository.GetLastPositionAsync(tableId, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        string position = await _service.ComputeInsertPositionAsync(tableId, null, null, CancellationToken.None);

        position.ShouldBe(FractionalIndex.Initial);
    }

    [Fact]
    public async Task ComputeInsertPositionWhenAppendingToNonEmptyTableShouldPlaceAfterCurrentLastPosition()
    {
        var tableId = Guid.CreateVersion7();

        _recordRepository.GetLastPositionAsync(tableId, Arg.Any<CancellationToken>())
            .Returns("a0");

        string position = await _service.ComputeInsertPositionAsync(tableId, null, null, CancellationToken.None);

        string.CompareOrdinal("a0", position).ShouldBeLessThan(0);
    }

    [Fact]
    public async Task ComputeInsertPositionWhenInsertingBetweenAdjacentRecordsShouldComputeMidpoint()
    {
        var tableId = Guid.CreateVersion7();
        var prevId = Guid.CreateVersion7();
        var nextId = Guid.CreateVersion7();

        _recordRepository.GetPositionInfosByIdsAsync(
                tableId,
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, CustomRecordPositionInfo>
            {
                [prevId] = new(prevId, tableId, "a0"),
                [nextId] = new(nextId, tableId, "a1"),
            });

        _recordRepository.HasRecordInPositionRangeAsync(tableId, "a0", "a1", null, Arg.Any<CancellationToken>())
            .Returns(false);

        string position = await _service.ComputeInsertPositionAsync(tableId, prevId, nextId, CancellationToken.None);

        string.CompareOrdinal("a0", position).ShouldBeLessThan(0);
        string.CompareOrdinal(position, "a1").ShouldBeLessThan(0);
    }

    [Fact]
    public async Task ComputeInsertPositionWhenNeighborsAreNotAdjacentShouldThrowConcurrencyConflictException()
    {
        var tableId = Guid.CreateVersion7();
        var prevId = Guid.CreateVersion7();
        var nextId = Guid.CreateVersion7();

        _recordRepository.GetPositionInfosByIdsAsync(
                tableId,
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, CustomRecordPositionInfo>
            {
                [prevId] = new(prevId, tableId, "a0"),
                [nextId] = new(nextId, tableId, "a2"),
            });

        _recordRepository.HasRecordInPositionRangeAsync(tableId, "a0", "a2", null, Arg.Any<CancellationToken>())
            .Returns(true);

        await Should.ThrowAsync<ConcurrencyConflictException>(() =>
            _service.ComputeInsertPositionAsync(tableId, prevId, nextId, CancellationToken.None));
    }

    [Fact]
    public async Task ComputeInsertPositionWhenNeighborDoesNotExistShouldThrowEntityNotFoundException()
    {
        var tableId = Guid.CreateVersion7();
        var missingNeighborId = Guid.CreateVersion7();

        _recordRepository.GetPositionInfosByIdsAsync(
                tableId,
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, CustomRecordPositionInfo>());

        await Should.ThrowAsync<EntityNotFoundException<CustomRecord>>(() =>
            _service.ComputeInsertPositionAsync(tableId, missingNeighborId, null, CancellationToken.None));
    }

    [Fact]
    public async Task ComputeInsertPositionWhenRankExceedsRebalanceThresholdShouldRebalanceAndRecomputeCompactRank()
    {
        var tableId = Guid.CreateVersion7();
        var prevId = Guid.CreateVersion7();
        var nextId = Guid.CreateVersion7();

        string deepPrev = "a0" + new string('z', FractionalIndex.RebalanceThresholdLength);
        string deepNext = "a1";

        _recordRepository.GetOrderedIdsAsync(tableId, Arg.Any<CancellationToken>())
            .Returns([prevId, nextId]);

        _recordRepository.GetPositionInfosByIdsAsync(
                tableId,
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(
                new Dictionary<Guid, CustomRecordPositionInfo>
                {
                    [prevId] = new(prevId, tableId, deepPrev),
                    [nextId] = new(nextId, tableId, deepNext),
                },
                new Dictionary<Guid, CustomRecordPositionInfo>
                {
                    [prevId] = new(prevId, tableId, "a0"),
                    [nextId] = new(nextId, tableId, "a1"),
                });

        _recordRepository.HasRecordInPositionRangeAsync(
                tableId,
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                null,
                Arg.Any<CancellationToken>())
            .Returns(false);

        string position = await _service.ComputeInsertPositionAsync(tableId, prevId, nextId, CancellationToken.None);

        await _recordRepository.Received(1).ReplaceAllPositionsAsync(
            tableId,
            Arg.Is<IReadOnlyDictionary<Guid, string>>(map =>
                map.Count == 2
                && string.CompareOrdinal(map[prevId], map[nextId]) < 0),
            Arg.Any<CancellationToken>());

        position.Length.ShouldBeLessThanOrEqualTo(4);
        string.CompareOrdinal("a0", position).ShouldBeLessThan(0);
        string.CompareOrdinal(position, "a1").ShouldBeLessThan(0);
    }

    [Fact]
    public async Task MoveWhenRecordAlreadyAtTailAndTargetIsTailShouldBeNoOp()
    {
        var tableId = Guid.CreateVersion7();
        var recordId = Guid.CreateVersion7();
        var info = new CustomRecordPositionInfo(recordId, tableId, "a2");

        _recordRepository.GetPositionInfoAsync(recordId, Arg.Any<CancellationToken>())
            .Returns(info);
        _recordRepository.HasRecordInPositionRangeAsync(tableId, "a2", null, recordId, Arg.Any<CancellationToken>())
            .Returns(false);

        await _service.MoveAsync(recordId, null, null, CancellationToken.None);

        await _recordRepository.DidNotReceive()
            .UpdatePositionAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoveWhenRecordAlreadyBetweenSpecifiedNeighborsShouldBeNoOp()
    {
        var tableId = Guid.CreateVersion7();
        var prevId = Guid.CreateVersion7();
        var recordId = Guid.CreateVersion7();
        var nextId = Guid.CreateVersion7();

        _recordRepository.GetPositionInfoAsync(recordId, Arg.Any<CancellationToken>())
            .Returns(new CustomRecordPositionInfo(recordId, tableId, "a1"));
        _recordRepository.GetPositionInfosByIdsAsync(
                tableId,
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, CustomRecordPositionInfo>
            {
                [prevId] = new(prevId, tableId, "a0"),
                [nextId] = new(nextId, tableId, "a2"),
            });
        _recordRepository.HasRecordInPositionRangeAsync(tableId, "a0", "a2", recordId, Arg.Any<CancellationToken>())
            .Returns(false);

        await _service.MoveAsync(recordId, prevId, nextId, CancellationToken.None);

        await _recordRepository.DidNotReceive()
            .UpdatePositionAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoveWhenValidTargetPositionShouldUpdatePosition()
    {
        var tableId = Guid.CreateVersion7();
        var recordId = Guid.CreateVersion7();
        var headId = Guid.CreateVersion7();

        _recordRepository.GetPositionInfoAsync(recordId, Arg.Any<CancellationToken>())
            .Returns(new CustomRecordPositionInfo(recordId, tableId, "a2"));
        _recordRepository.GetPositionInfosByIdsAsync(
                tableId,
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, CustomRecordPositionInfo>
            {
                [headId] = new(headId, tableId, "a0"),
            });
        _recordRepository.HasRecordInPositionRangeAsync(tableId, null, "a0", recordId, Arg.Any<CancellationToken>())
            .Returns(false);

        string? capturedPosition = null;
        _recordRepository.UpdatePositionAsync(
                recordId,
                Arg.Do<string>(p => capturedPosition = p),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _service.MoveAsync(recordId, null, headId, CancellationToken.None);

        capturedPosition.ShouldNotBeNull();
        string.CompareOrdinal(capturedPosition, "a0").ShouldBeLessThan(0);
    }

    [Fact]
    public async Task MoveWhenSelfReferencedOrInvalidNeighborIdsShouldThrowDomainValidationException()
    {
        var recordId = Guid.CreateVersion7();
        var sameId = Guid.CreateVersion7();

        await Should.ThrowAsync<DomainValidationException>(() =>
            _service.MoveAsync(recordId, recordId, null, CancellationToken.None));
        await Should.ThrowAsync<DomainValidationException>(() =>
            _service.MoveAsync(recordId, null, recordId, CancellationToken.None));
        await Should.ThrowAsync<DomainValidationException>(() =>
            _service.MoveAsync(recordId, sameId, sameId, CancellationToken.None));
        await Should.ThrowAsync<DomainValidationException>(() =>
            _service.MoveAsync(recordId, Guid.Empty, null, CancellationToken.None));
        await Should.ThrowAsync<DomainValidationException>(() =>
            _service.MoveAsync(recordId, null, Guid.Empty, CancellationToken.None));
    }

    [Fact]
    public async Task CreateCustomRecordHandlerShouldLockTableBeforeReadingSchemaAndPersistComputedPosition()
    {
        var tableId = Guid.CreateVersion7();
        var field = new CustomField { Name = "City", IsRequired = true, Order = 0 };
        var positionService = Substitute.For<ICustomRecordPositionService>();

        _tableRepository.GetFieldsByCustomTableIdAsync(tableId, Arg.Any<CancellationToken>())
            .Returns([field]);
        positionService.ComputeInsertPositionAsync(tableId, null, null, Arg.Any<CancellationToken>())
            .Returns("a0");

        var handler = new CreateCustomRecordCommandHandler(
            _tableRepository,
            _recordRepository,
            positionService,
            NullLogger<CreateCustomRecordCommandHandler>.Instance);

        var command = new CreateCustomRecordCommand(
            tableId,
            new JsonObject { [field.Id.ToString()] = "Kyiv" });

        await handler.Handle(command, CancellationToken.None);

        Received.InOrder(() =>
        {
            _tableRepository.LockForUpdateAsync(tableId, Arg.Any<CancellationToken>());
            _tableRepository.GetFieldsByCustomTableIdAsync(tableId, Arg.Any<CancellationToken>());
            positionService.ComputeInsertPositionAsync(tableId, null, null, Arg.Any<CancellationToken>());
            _recordRepository.CreateAsync(
                Arg.Is<CustomRecord>(r => r.CustomTableId == tableId && r.Position == "a0"),
                Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task CreateCustomRecordHandlerWhenTableHasNoFieldsShouldExplicitlyVerifyTableExistence()
    {
        var tableId = Guid.CreateVersion7();
        var positionService = Substitute.For<ICustomRecordPositionService>();

        _tableRepository.GetFieldsByCustomTableIdAsync(tableId, Arg.Any<CancellationToken>())
            .Returns([]);
        _tableRepository.EnsureExistsAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new EntityNotFoundException<CustomTable>(tableId)));

        var handler = new CreateCustomRecordCommandHandler(
            _tableRepository,
            _recordRepository,
            positionService,
            NullLogger<CreateCustomRecordCommandHandler>.Instance);

        await Should.ThrowAsync<EntityNotFoundException<CustomTable>>(() =>
            handler.Handle(new CreateCustomRecordCommand(tableId, []), CancellationToken.None).AsTask());

        await _tableRepository.Received(1).LockForUpdateAsync(tableId, Arg.Any<CancellationToken>());
        await _tableRepository.Received(1).EnsureExistsAsync(tableId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateCustomRecordHandlerShouldLockParentTableBeforeLoadingRecordAndSchema()
    {
        var tableId = Guid.CreateVersion7();
        var recordId = Guid.CreateVersion7();
        var field = new CustomField { Name = "City", IsRequired = true, Order = 0 };
        var record = new CustomRecord { Id = recordId, CustomTableId = tableId, Position = "a0", Data = [] };

        _recordRepository.GetPositionInfoAsync(recordId, Arg.Any<CancellationToken>())
            .Returns(new CustomRecordPositionInfo(recordId, tableId, "a0"));
        _recordRepository.GetForUpdateAsync(recordId, Arg.Any<CancellationToken>())
            .Returns(record);
        _tableRepository.GetFieldsByCustomTableIdAsync(tableId, Arg.Any<CancellationToken>())
            .Returns([field]);

        var handler = new UpdateCustomRecordCommandHandler(
            _recordRepository,
            _tableRepository,
            NullLogger<UpdateCustomRecordCommandHandler>.Instance);

        var command = new UpdateCustomRecordCommand(new JsonObject { [field.Id.ToString()] = "Lviv" })
        {
            Id = recordId,
        };

        await handler.Handle(command, CancellationToken.None);

        Received.InOrder(() =>
        {
            _recordRepository.GetPositionInfoAsync(recordId, Arg.Any<CancellationToken>());
            _tableRepository.LockForUpdateAsync(tableId, Arg.Any<CancellationToken>());
            _recordRepository.GetForUpdateAsync(recordId, Arg.Any<CancellationToken>());
            _tableRepository.GetFieldsByCustomTableIdAsync(tableId, Arg.Any<CancellationToken>());
        });

        record.Data[field.Id.ToString()]?.GetValue<string>().ShouldBe("Lviv");
    }

    [Fact]
    public void CommandValidatorsShouldRejectInvalidNeighborIdentifiers()
    {
        var tableId = Guid.CreateVersion7();
        var sameId = Guid.CreateVersion7();

        var createValidator = new CreateCustomRecordCommandValidator();
        createValidator.Validate(new CreateCustomRecordCommand(tableId, [], sameId, sameId))
            .IsValid.ShouldBeFalse();
        createValidator.Validate(new CreateCustomRecordCommand(tableId, [], Guid.Empty, null))
            .IsValid.ShouldBeFalse();
        createValidator.Validate(new CreateCustomRecordCommand(tableId, [], Guid.CreateVersion7(), Guid.CreateVersion7()))
            .IsValid.ShouldBeTrue();

        var recordId = Guid.CreateVersion7();
        var moveValidator = new MoveCustomRecordCommandValidator();
        moveValidator.Validate(new MoveCustomRecordCommand(recordId, null) { Id = recordId })
            .IsValid.ShouldBeFalse();
        moveValidator.Validate(new MoveCustomRecordCommand(null, recordId) { Id = recordId })
            .IsValid.ShouldBeFalse();
        moveValidator.Validate(new MoveCustomRecordCommand(sameId, sameId) { Id = recordId })
            .IsValid.ShouldBeFalse();
        moveValidator.Validate(new MoveCustomRecordCommand(null, null) { Id = Guid.Empty })
            .IsValid.ShouldBeFalse();
        moveValidator.Validate(new MoveCustomRecordCommand(null, Guid.CreateVersion7()) { Id = recordId })
            .IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidationBehaviorShouldRejectEmptyEntityIdForIHasEntityIdRequests()
    {
        var deleteBehavior = new ValidationBehavior<DeleteCustomRecordCommand, Mediator.Unit>([]);
        var queryBehavior = new ValidationBehavior<GetCustomRecordByIdQuery, CustomRecordDto>([]);
        var tableQueryBehavior = new ValidationBehavior<GetCustomTableByIdQuery, CustomTableDto>([]);

        await Should.ThrowAsync<ValidationException>(async () =>
            await deleteBehavior.Handle(
                new DeleteCustomRecordCommand(Guid.Empty),
                (_, _) => ValueTask.FromResult(Mediator.Unit.Value),
                CancellationToken.None));

        await Should.ThrowAsync<ValidationException>(async () =>
            await queryBehavior.Handle(
                new GetCustomRecordByIdQuery(Guid.Empty),
                (_, _) => throw new InvalidOperationException("Should not be invoked"),
                CancellationToken.None));

        await Should.ThrowAsync<ValidationException>(async () =>
            await tableQueryBehavior.Handle(
                new GetCustomTableByIdQuery(Guid.Empty),
                (_, _) => throw new InvalidOperationException("Should not be invoked"),
                CancellationToken.None));
    }
}
