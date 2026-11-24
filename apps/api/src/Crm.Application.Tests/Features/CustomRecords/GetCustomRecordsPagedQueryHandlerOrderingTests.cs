using Crm.Application.Dtos.CustomRecords;
using Crm.Application.Features.CustomRecords.Queries;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
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
    private readonly ICustomFieldRepository _fieldRepo = Substitute.For<ICustomFieldRepository>();
    private readonly ICustomRecordRepository _recordRepo = Substitute.For<ICustomRecordRepository>();
    private readonly ILogger<GetCustomRecordsPagedQueryHandler> _logger = Substitute.For<ILogger<GetCustomRecordsPagedQueryHandler>>();

    /// <summary>
    /// Verifies that sorting by <see cref="CustomRecord.CustomTableId"/> preserves the property ordering
    /// and checks table existence when no custom fields are returned.
    /// </summary>
    [Fact]
    public async Task HandleWhenSortingByCustomTableIdShouldPreserveCustomTableIdOrdering()
    {
        // Arrange
        var handler = new GetCustomRecordsPagedQueryHandler(_tableRepo, _fieldRepo, _recordRepo, _logger);
        var tableId = Guid.NewGuid();

        var query = new GetCustomRecordsPagedQuery(tableId, OrderBy: nameof(CustomRecord.CustomTableId), SortOrder: "DESC");

        _fieldRepo.GetByCustomTableIdAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CustomField>>([]));

        _recordRepo.GetPagedAsync<CustomRecordDto>(
                tableId,
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<FieldDataType?>(),
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
            fieldDataType: null,
            sortOrder: "DESC",
            pageNumber: Arg.Any<int>(),
            pageSize: Arg.Any<int>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that sorting by a valid custom field passes the field key and data type
    /// and skips the redundant table existence check when custom fields exist.
    /// </summary>
    [Fact]
    public async Task HandleWhenSortingByValidCustomFieldShouldPassCustomFieldKeyAndDataType()
    {
        // Arrange
        var handler = new GetCustomRecordsPagedQueryHandler(_tableRepo, _fieldRepo, _recordRepo, _logger);
        var tableId = Guid.NewGuid();

        var query = new GetCustomRecordsPagedQuery(tableId, OrderBy: "data['age']", SortOrder: "ASC");

        _fieldRepo.GetByCustomTableIdAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CustomField>>([new CustomField { Key = "age", DataType = FieldDataType.Number }]));

        _recordRepo.GetPagedAsync<CustomRecordDto>(
                tableId,
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<FieldDataType?>(),
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
            orderBy: "age",
            fieldDataType: FieldDataType.Number,
            sortOrder: "ASC",
            pageNumber: Arg.Any<int>(),
            pageSize: Arg.Any<int>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that sorting by a nonexistent dynamic field falls back to <see cref="CustomRecordDto.Id"/>.
    /// </summary>
    [Fact]
    public async Task HandleWhenSortingByGhostFieldShouldFallbackToId()
    {
        // Arrange
        var handler = new GetCustomRecordsPagedQueryHandler(_tableRepo, _fieldRepo, _recordRepo, _logger);
        var tableId = Guid.NewGuid();

        var query = new GetCustomRecordsPagedQuery(tableId, OrderBy: "data.nonexistent_field", SortOrder: "ASC");

        _fieldRepo.GetByCustomTableIdAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CustomField>>([new CustomField { Key = "age", DataType = FieldDataType.Number }]));

        _recordRepo.GetPagedAsync<CustomRecordDto>(
                tableId,
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<FieldDataType?>(),
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
            fieldDataType: null,
            sortOrder: "ASC",
            pageNumber: Arg.Any<int>(),
            pageSize: Arg.Any<int>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that sorting by a navigation property falls back to <see cref="CustomRecordDto.Id"/>.
    /// </summary>
    [Fact]
    public async Task HandleWhenSortingByNavigationPropertyShouldFallbackToId()
    {
        // Arrange
        var handler = new GetCustomRecordsPagedQueryHandler(_tableRepo, _fieldRepo, _recordRepo, _logger);
        var tableId = Guid.NewGuid();

        var query = new GetCustomRecordsPagedQuery(tableId, OrderBy: nameof(CustomRecord.CustomTable), SortOrder: "ASC");

        _fieldRepo.GetByCustomTableIdAsync(tableId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CustomField>>([]));

        _recordRepo.GetPagedAsync<CustomRecordDto>(
                tableId,
                Arg.Any<string?>(),
                Arg.Any<string>(),
                Arg.Any<FieldDataType?>(),
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
            fieldDataType: null,
            sortOrder: "ASC",
            pageNumber: Arg.Any<int>(),
            pageSize: Arg.Any<int>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }
}
