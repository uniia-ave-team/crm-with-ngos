using Crm.Application.Dtos.CustomRecords;
using Crm.Application.Dtos.CustomRecords.Queries;
using Crm.Application.Features.CustomRecords.Queries;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Crm.Application.Tests.Features.CustomRecords;

public class GetAllCustomRecordsQueryHandlerOrderingTests
{
    private readonly ICustomTableRepository _tableRepo = Substitute.For<ICustomTableRepository>();
    private readonly ICustomFieldRepository _fieldRepo = Substitute.For<ICustomFieldRepository>();
    private readonly ICustomRecordRepository _recordRepo = Substitute.For<ICustomRecordRepository>();
    private readonly ILogger<GetAllCustomRecordsQueryHandler> _logger = Substitute.For<ILogger<GetAllCustomRecordsQueryHandler>>();

    [Fact]
    public async Task HandleWhenSortingByCustomTableIdShouldPreserveCustomTableIdOrdering()
    {
        // Arrange
        var handler = new GetAllCustomRecordsQueryHandler(_tableRepo, _fieldRepo, _recordRepo, _logger);
        var tableId = Guid.NewGuid();

        var query = new GetAllCustomRecordsQuery(tableId, OrderBy: nameof(CustomRecord.CustomTableId), SortOrder: "DESC");

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

    [Fact]
    public async Task HandleWhenSortingByValidCustomFieldShouldPassCustomFieldKeyAndDataType()
    {
        // Arrange
        var handler = new GetAllCustomRecordsQueryHandler(_tableRepo, _fieldRepo, _recordRepo, _logger);
        var tableId = Guid.NewGuid();

        var query = new GetAllCustomRecordsQuery(tableId, OrderBy: "data['age']", SortOrder: "ASC");

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

    [Fact]
    public async Task HandleWhenSortingByGhostFieldShouldFallbackToId()
    {
        // Arrange
        var handler = new GetAllCustomRecordsQueryHandler(_tableRepo, _fieldRepo, _recordRepo, _logger);
        var tableId = Guid.NewGuid();

        var query = new GetAllCustomRecordsQuery(tableId, OrderBy: "data.nonexistent_field", SortOrder: "ASC");

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

    [Fact]
    public async Task HandleWhenSortingByNavigationPropertyShouldFallbackToId()
    {
        // Arrange
        var handler = new GetAllCustomRecordsQueryHandler(_tableRepo, _fieldRepo, _recordRepo, _logger);
        var tableId = Guid.NewGuid();

        var query = new GetAllCustomRecordsQuery(tableId, OrderBy: nameof(CustomRecord.CustomTable), SortOrder: "ASC");

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
