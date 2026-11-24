using Crm.Application.Common.Consts;
using Crm.Application.Dtos.TableTemplates;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.TableTemplates.Queries;

/// <summary>
/// Handles the <see cref="GetTableTemplateByIdQuery"/> to retrieve a table template by its unique identifier.
/// </summary>
/// <param name="tableTemplateRepository">The repository used for accessing table template data.</param>
/// <param name="logger">The logger instance for tracking query execution.</param>
public sealed partial class GetTableTemplateByIdQueryHandler(
    ITableTemplateRepository tableTemplateRepository,
    ILogger<GetTableTemplateByIdQueryHandler> logger) : IQueryHandler<GetTableTemplateByIdQuery, TableTemplateDto>
{
    /// <inheritdoc />
    public async ValueTask<TableTemplateDto> Handle(GetTableTemplateByIdQuery query, CancellationToken cancellationToken)
    {
        LogFetchingTableTemplateById(logger, query.Id);

        var template = await tableTemplateRepository.GetAsync<TableTemplateDto>(query.Id, cancellationToken);

        LogTableTemplateByIdFetched(logger, query.Id);

        return template;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingTableTemplateById, Level = LogLevel.Information, Message = "Fetching table template with ID: {TemplateId}")]
    private static partial void LogFetchingTableTemplateById(ILogger logger, Guid templateId);

    [LoggerMessage(EventId = LogEventIds.TableTemplateByIdFetched, Level = LogLevel.Information, Message = "Successfully fetched table template with ID: {TemplateId}")]
    private static partial void LogTableTemplateByIdFetched(ILogger logger, Guid templateId);
}
