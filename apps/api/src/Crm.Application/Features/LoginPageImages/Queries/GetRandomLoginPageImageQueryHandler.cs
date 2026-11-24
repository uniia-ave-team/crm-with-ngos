using Crm.Application.Common.Consts;
using Crm.Application.Dtos.LoginPageImages;
using Crm.Application.Features.LoginPageImages.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.LoginPageImages.Queries;

/// <summary>
/// Handles the <see cref="GetRandomLoginPageImageQuery"/> to randomly select and retrieve one login page image.
/// </summary>
/// <param name="repository">The repository used to manage data access operations for login page images.</param>
/// <param name="fileUrlProvider">The provider used to construct the full web URL for the login page image.</param>
/// <param name="logger">The logger used to record the execution and outcome of the random image retrieval process.</param>
public sealed partial class GetRandomLoginPageImageQueryHandler(
    ILoginPageImageRepository repository,
    IFileUrlProvider fileUrlProvider,
    ILogger<GetRandomLoginPageImageQueryHandler> logger) : IQueryHandler<GetRandomLoginPageImageQuery, LoginPageImageDto>
{
    public async ValueTask<LoginPageImageDto> Handle(GetRandomLoginPageImageQuery query, CancellationToken cancellationToken)
    {
        LogFetchingRandomImage(logger);

        var dto = await repository.GetRandomAsync<LoginPageImageDto>(cancellationToken);

        dto = dto.WithResolvedUrl(fileUrlProvider);

        LogFetchedRandomImageSuccessfully(logger, dto.Id);

        return dto;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingRandomLoginPageImage, Level = LogLevel.Information, Message = "Fetching a random login page image.")]
    private static partial void LogFetchingRandomImage(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.RandomLoginPageImageFetched, Level = LogLevel.Information, Message = "Successfully fetched random login page image with ID: {Id}.")]
    private static partial void LogFetchedRandomImageSuccessfully(ILogger logger, Guid id);
}
