using Crm.Application.Dtos.CustomRecords;
using Crm.Application.Features.CustomRecords.Queries;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Crm.Application.Tests.Features.CustomRecords;

/// <summary>
/// Contains unit tests for <see cref="GetCustomRecordsPagedQueryHandler"/> ordering resolution and table existence checks.
/// </summary>
public class GetCustomRecordsPagedQueryHandlerOrderingTests
{
    private readonly ICustomTableRepository _tableRepo = Substitute.For<ICustomTableRepository>();
    private readonly ICustomRecordRepository _recordRepo = Substitute.For<ICustomRecordRepository>();
    private readonly ILogger<GetCustomRecordsPagedQueryHandler> _logger = Substitute.For<ILogger<GetCustomRecordsPagedQueryHandler>>();

    [Fact]
    public async Task HandleWhenOrderByIsOmittedShouldPassNullForManualPositionOrdering()
    {
        // Arrange
        var handler = new GetCustomRecordsPagedQueryHandler(_tableRepo, _recordRepo, _logger);
        var tableId = Guid.CreateVersion7();
        var query = new GetCustomRecordsPagedQuery(tableId);

        _tableRepo.GetFieldsByCustomTableIdAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CustomField>>([new CustomField { Name = "Age" }]));

        _recordRepo.GetPagedAsync<CustomRecordDto>(
                tableId,
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<CustomRecordDto>([], 0, 1, 20)));

        // Act
        await handler.Handle(query, CancellationToken.None);

        // Assert
        await _recordRepo.Received(1).GetPagedAsync<CustomRecordDto>(
            customTableId: tableId,
            searchTerm: Arg.Any<string?>(),
            orderBy: null,
            sortOrder: "asc",
            pageNumber: Arg.Any<int>(),
            pageSize: Arg.Any<int>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWhenSortingByCustomTableIdShouldPreserveCustomTableIdOrdering()
    {
        // Arrange
        var handler = new GetCustomRecordsPagedQueryHandler(_tableRepo, _recordRepo, _logger);
        var tableId = Guid.CreateVersion7();
        var query = new GetCustomRecordsPagedQuery(tableId, OrderBy: nameof(CustomRecord.CustomTableId), SortOrder: "DESC");

        _tableRepo.GetFieldsByCustomTableIdAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CustomField>>([]));

        _recordRepo.GetPagedAsync<CustomRecordDto>(
                tableId,
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<CustomRecordDto>([], 0, 1, 20)));

        // Act
        await handler.Handle(query, CancellationToken.None);

        // Assert
        await _tableRepo.Received(1).EnsureExistsAsync(tableId, Arg.Any<CancellationToken>());
        await _recordRepo.Received(1).GetPagedAsync<CustomRecordDto>(
            customTableId: tableId,
            searchTerm: Arg.Any<string?>(),
            orderBy: nameof(CustomRecord.CustomTableId),
            sortOrder: "DESC",
            pageNumber: Arg.Any<int>(),
            pageSize: Arg.Any<int>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWhenSortingByValidCustomFieldIdShouldPassCanonicalLowercaseFieldId()
    {
        // Arrange
        var ageField = new CustomField { Name = "Age" };
        var handler = new GetCustomRecordsPagedQueryHandler(_tableRepo, _recordRepo, _logger);
        var tableId = Guid.CreateVersion7();
        var query = new GetCustomRecordsPagedQuery(tableId, OrderBy: ageField.Id.ToString().ToUpperInvariant(), SortOrder: "ASC");

        _tableRepo.GetFieldsByCustomTableIdAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CustomField>>([ageField]));

        _recordRepo.GetPagedAsync<CustomRecordDto>(
                tableId,
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<CustomRecordDto>([], 0, 1, 20)));

        // Act
        await handler.Handle(query, CancellationToken.None);

        // Assert
        await _tableRepo.DidNotReceive().EnsureExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _recordRepo.Received(1).GetPagedAsync<CustomRecordDto>(
            customTableId: tableId,
            searchTerm: Arg.Any<string?>(),
            orderBy: ageField.Id.ToString(),
            sortOrder: "ASC",
            pageNumber: Arg.Any<int>(),
            pageSize: Arg.Any<int>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWhenSortingByGhostOrPrefixedFieldShouldFallbackToId()
    {
        // Arrange
        var handler = new GetCustomRecordsPagedQueryHandler(_tableRepo, _recordRepo, _logger);
        var tableId = Guid.CreateVersion7();
        var query = new GetCustomRecordsPagedQuery(tableId, OrderBy: "data.nonexistent_field", SortOrder: "ASC");

        _tableRepo.GetFieldsByCustomTableIdAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CustomField>>([new CustomField { Name = "Age" }]));

        _recordRepo.GetPagedAsync<CustomRecordDto>(
                tableId,
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<CustomRecordDto>([], 0, 1, 20)));

        // Act
        await handler.Handle(query, CancellationToken.None);

        // Assert
        await _tableRepo.DidNotReceive().EnsureExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _recordRepo.Received(1).GetPagedAsync<CustomRecordDto>(
            customTableId: tableId,
            searchTerm: Arg.Any<string?>(),
            orderBy: nameof(CustomRecordDto.Id),
            sortOrder: "ASC",
            pageNumber: Arg.Any<int>(),
            pageSize: Arg.Any<int>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWhenSortingByNavigationPropertyShouldFallbackToId()
    {
        // Arrange
        var handler = new GetCustomRecordsPagedQueryHandler(_tableRepo, _recordRepo, _logger);
        var tableId = Guid.CreateVersion7();
        var query = new GetCustomRecordsPagedQuery(tableId, OrderBy: nameof(CustomRecord.CustomTable), SortOrder: "ASC");

        _tableRepo.GetFieldsByCustomTableIdAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CustomField>>([]));

        _recordRepo.GetPagedAsync<CustomRecordDto>(
                tableId,
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<CustomRecordDto>([], 0, 1, 20)));

        // Act
        await handler.Handle(query, CancellationToken.None);

        // Assert
        await _tableRepo.Received(1).EnsureExistsAsync(tableId, Arg.Any<CancellationToken>());
        await _recordRepo.Received(1).GetPagedAsync<CustomRecordDto>(
            customTableId: tableId,
            searchTerm: Arg.Any<string?>(),
            orderBy: nameof(CustomRecordDto.Id),
            sortOrder: "ASC",
            pageNumber: Arg.Any<int>(),
            pageSize: Arg.Any<int>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }
}
