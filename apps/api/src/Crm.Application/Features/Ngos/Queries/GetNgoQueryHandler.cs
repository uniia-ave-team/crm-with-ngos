using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Ngo;
using Crm.Application.Dtos.Ngo.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Ngos.Queries;

/// <summary>
/// Handles the <see cref="GetNgoQuery"/> to fetch and map the NGO details.
/// </summary>
/// <param name="repository">The repository used for accessing NGO data.</param>
/// <param name="fileUrlProvider">The provider used to construct the full web URL for the NGO's logo.</param>
/// <param name="logger">The logger instance for tracking query execution.</param>
public partial class GetNgoQueryHandler(
    INgoRepository repository,
    IFileUrlProvider fileUrlProvider,
    ILogger<GetNgoQueryHandler> logger) : IRequestHandler<GetNgoQuery, NgoDto?>
{
    /// <summary>
    /// Handles the retrieval process for the NGO asynchronously.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation, containing the mapped <see cref="NgoDto"/>.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the NGO is not found in the database.</exception>
    public async Task<NgoDto?> Handle(GetNgoQuery request, CancellationToken cancellationToken)
    {
        LogFetchingNgoDetails(logger);

        var ngoDto = await repository.GetAsync<NgoDto>(cancellationToken);

        if (!string.IsNullOrWhiteSpace(ngoDto.LogoUrl))
        {
            ngoDto = ngoDto with
            {
                LogoUrl = fileUrlProvider.GetFileUrl(ngoDto.LogoUrl, ApiRouteLogoConstants.NgoLogo),
            };
        }

        LogNgoDetailsFetchedSuccessfully(logger, ngoDto.Id);

        return ngoDto;
    }

    [LoggerMessage(
        EventId = LogEventIds.FetchingNgoDetails, Level = LogLevel.Information, Message = "Fetching NGO details.")]
    private static partial void LogFetchingNgoDetails(ILogger logger);

    [LoggerMessage(
        EventId = LogEventIds.NgoDetailsFetched, Level = LogLevel.Information, Message = "NGO details fetched successfully for NGO ID: {NgoId}")]
    private static partial void LogNgoDetailsFetchedSuccessfully(ILogger logger, Guid ngoId);
}
