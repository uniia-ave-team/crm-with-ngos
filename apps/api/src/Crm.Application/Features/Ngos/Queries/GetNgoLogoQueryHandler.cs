using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Common;
using Crm.Application.Dtos.Ngo.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Ngos.Queries;

/// <summary>
/// Handles the <see cref="GetNgoLogoQuery"/> to retrieve the full public URL of the current NGO's logo.
/// </summary>
/// <param name="ngoRepository">The repository used for accessing the NGO record.</param>
/// <param name="fileStorageService">The service responsible for retrieving the logo file from storage.</param>
/// <param name="logger">The logger instance for tracking query execution.</param>
public partial class GetNgoLogoQueryHandler(
    INgoRepository ngoRepository,
    IFileStorageService fileStorageService,
    ILogger<GetNgoLogoQueryHandler> logger) : IRequestHandler<GetNgoLogoQuery, FileDto>
{
    public async Task<FileDto> Handle(GetNgoLogoQuery request, CancellationToken cancellationToken)
    {
        LogFetchingNgoLogo(logger);

        var logo = await ngoRepository.GetLogoAsync(cancellationToken);

        var stream = await fileStorageService.GetFileAsync(logo, FileStorageConstants.NgosFolder, cancellationToken);

        LogNgoLogoFetched(logger);

        return new(stream, logo);
    }

    [LoggerMessage(EventId = LogEventIds.FetchingNgoLogo, Level = LogLevel.Debug, Message = "Fetching current NGO logo URL.")]
    private static partial void LogFetchingNgoLogo(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.NgoLogoFetched, Level = LogLevel.Debug, Message = "NGO logo query completed.")]
    private static partial void LogNgoLogoFetched(ILogger logger);
}
