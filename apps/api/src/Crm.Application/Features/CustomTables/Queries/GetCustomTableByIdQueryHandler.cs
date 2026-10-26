using Crm.Application.Common.Consts;
using Crm.Application.Dtos.CustomTables;
using Crm.Application.Dtos.CustomTables.Queries;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomTables.Queries;

/// <summary>
/// Handles the <see cref="GetCustomTableByIdQuery"/> to retrieve a custom table schema definition along with all its fields.
/// </summary>
/// <param name="customTableRepository">The repository used for retrieving custom table data.</param>
/// <param name="logger">The logger instance for tracking query execution.</param>
public partial class GetCustomTableByIdQueryHandler(
    ICustomTableRepository customTableRepository,
    ILogger<GetCustomTableByIdQueryHandler> logger) : IRequestHandler<GetCustomTableByIdQuery, CustomTableDto>
{
    public async Task<CustomTableDto> Handle(GetCustomTableByIdQuery request, CancellationToken cancellationToken)
    {
        LogFetchingCustomTable(logger, request.Id);

        var customTableDto = await customTableRepository.GetAsync<CustomTableDto>(request.Id, cancellationToken);

        LogCustomTableFetchedSuccessfully(logger, request.Id);

        return customTableDto;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingCustomTableById, Level = LogLevel.Information, Message = "Initiating retrieval of custom table ID: {TableId}")]
    private static partial void LogFetchingCustomTable(ILogger logger, Guid tableId);

    [LoggerMessage(EventId = LogEventIds.CustomTableByIdFetched, Level = LogLevel.Information, Message = "Custom table successfully retrieved for table ID: {TableId}")]
    private static partial void LogCustomTableFetchedSuccessfully(ILogger logger, Guid tableId);
}
