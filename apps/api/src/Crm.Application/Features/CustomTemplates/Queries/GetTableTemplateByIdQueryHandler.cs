using Crm.Application.Common.Consts;
using Crm.Application.Dtos.CustomTemplates;
using Crm.Application.Dtos.CustomTemplates.Queries;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomTemplates.Queries;

/// <summary>
/// Handles the <see cref="GetTableTemplateByIdQuery"/> to retrieve a table template by its unique identifier.
/// </summary>
/// <param name="tableTemplateRepository">The repository used for accessing table template data.</param>
/// <param name="logger">The logger instance for tracking query execution.</param>
public partial class GetTableTemplateByIdQueryHandler(
    ITableTemplateRepository tableTemplateRepository,
    ILogger<GetTableTemplateByIdQueryHandler> logger) : IRequestHandler<GetTableTemplateByIdQuery, TableTemplateDto>
{
    public async Task<TableTemplateDto> Handle(GetTableTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        LogFetchingTableTemplateById(logger, request.Id);

        var template = await tableTemplateRepository.GetAsync<TableTemplateDto>(request.Id, cancellationToken);

        LogTableTemplateByIdFetched(logger, request.Id);

        return template;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingTableTemplateById, Level = LogLevel.Information, Message = "Fetching table template with ID: {TemplateId}")]
    private static partial void LogFetchingTableTemplateById(ILogger logger, Guid templateId);

    [LoggerMessage(EventId = LogEventIds.TableTemplateByIdFetched, Level = LogLevel.Information, Message = "Successfully fetched table template with ID: {TemplateId}")]
    private static partial void LogTableTemplateByIdFetched(ILogger logger, Guid templateId);
}
