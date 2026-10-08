using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Common;
using Crm.Application.Dtos.LoginPageImage.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.LoginPageImages.Queries;

/// <summary>
/// Handles the <see cref="GetLoginPageImageFileQuery"/> to retrieve the file stream of a specific login page image.
/// </summary>
/// <param name="repository">The repository used for accessing the login page image record and filename.</param>
/// <param name="fileStorageService">The service responsible for physical file I/O operations.</param>
/// <param name="logger">The logger instance for tracking query execution.</param>
public partial class GetLoginPageImageFileQueryHandler(
    ILoginPageImageRepository repository,
    IFileStorageService fileStorageService,
    ILogger<GetLoginPageImageFileQueryHandler> logger) : IRequestHandler<GetLoginPageImageFileQuery, FileDto>
{
    public async Task<FileDto> Handle(GetLoginPageImageFileQuery request, CancellationToken cancellationToken)
    {
        LogFetchingLoginPageImageFile(logger, request.Id);

        var fileName = await repository.GetUrlAsync(request.Id, cancellationToken);

        var stream = await fileStorageService.GetFileAsync(fileName, FileStorageConstants.LoginPageImagesFolder, cancellationToken);

        LogLoginPageImageFileFetched(logger, request.Id);

        return new(stream, fileName);
    }

    [LoggerMessage(EventId = LogEventIds.FetchingLoginPageImageFile, Level = LogLevel.Debug, Message = "Fetching file for login page image ID: {Id}")]
    private static partial void LogFetchingLoginPageImageFile(ILogger logger, Guid id);

    [LoggerMessage(EventId = LogEventIds.LogLoginPageImageFileFetched, Level = LogLevel.Debug, Message = "Login page image file query completed successfully for ID: {Id}")]
    private static partial void LogLoginPageImageFileFetched(ILogger logger, Guid id);
}
