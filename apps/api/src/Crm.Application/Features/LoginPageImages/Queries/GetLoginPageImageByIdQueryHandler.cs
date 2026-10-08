using Crm.Application.Common.Consts;
using Crm.Application.Dtos.LoginPageImage;
using Crm.Application.Dtos.LoginPageImage.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.LoginPageImages.Queries;

/// <summary>
/// Handles the <see cref="GetLoginPageImageByIdQuery"/> to retrieve a specific login page image by its ID.
/// </summary>
/// <param name="repository">The repository used to manage data access operations for login page images.</param>
/// <param name="fileUrlProvider">The provider used to construct the full web URL for the login page image.</param>
/// <param name="logger">The logger used to record the execution and outcome of the image retrieval process.</param>
public partial class GetLoginPageImageByIdQueryHandler(
    ILoginPageImageRepository repository,
    IFileUrlProvider fileUrlProvider,
    ILogger<GetLoginPageImageByIdQueryHandler> logger) : IRequestHandler<GetLoginPageImageByIdQuery, LoginPageImageDto>
{
    public async Task<LoginPageImageDto> Handle(GetLoginPageImageByIdQuery request, CancellationToken cancellationToken)
    {
        LogFetchingImageById(logger, request.Id);

        var dto = await repository.GetAsync<LoginPageImageDto>(request.Id, cancellationToken);

        dto = dto with
        {
            Url = fileUrlProvider.GetFileUrl(dto.Url, ApiRouteLogoConstants.LoginPageImageFile(dto.Id)) ?? dto.Url,
        };

        LogFetchedImageByIdSuccessfully(logger, dto.Id);

        return dto;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingLoginPageImageById, Level = LogLevel.Information, Message = "Fetching login page image with ID: {Id}")]
    private static partial void LogFetchingImageById(ILogger logger, Guid id);

    [LoggerMessage(EventId = LogEventIds.LoginPageImageByIdFetchedSuccessfully, Level = LogLevel.Information, Message = "Successfully fetched login page image with ID: {Id}")]
    private static partial void LogFetchedImageByIdSuccessfully(ILogger logger, Guid id);
}
