using Crm.Application.Common.Consts;
using Crm.Application.Dtos.LoginPageImage;
using Crm.Application.Dtos.LoginPageImage.Queries;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.LoginPageImages.Queries;

/// <summary>
/// Handles the <see cref="GetRandomLoginPageImageQuery"/> to randomly select and retrieve one login page image.
/// </summary>
/// <param name="repository">The repository used to manage data access operations for login page images.</param>
/// <param name="logger">The logger used to record the execution and outcome of the random image retrieval process.</param>
public partial class GetRandomLoginPageImageQueryHandler(
    ILoginPageImageRepository repository,
    ILogger<GetRandomLoginPageImageQueryHandler> logger) : IRequestHandler<GetRandomLoginPageImageQuery, LoginPageImageDto>
{
    public async Task<LoginPageImageDto> Handle(GetRandomLoginPageImageQuery request, CancellationToken cancellationToken)
    {
        LogFetchingRandomImage(logger);

        var dto = await repository.GetRandomAsync<LoginPageImageDto>(cancellationToken);

        LogFetchedRandomImageSuccessfully(logger, dto.Id);

        return dto;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingRandomLoginPageImage, Level = LogLevel.Information, Message = "Fetching a random login page image.")]
    private static partial void LogFetchingRandomImage(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.RandomLoginPageImageFetched, Level = LogLevel.Information, Message = "Successfully fetched random login page image with ID: {Id}.")]
    private static partial void LogFetchedRandomImageSuccessfully(ILogger logger, Guid id);
}
