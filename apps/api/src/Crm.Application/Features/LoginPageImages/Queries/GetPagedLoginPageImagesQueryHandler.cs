using Crm.Application.Common.Consts;
using Crm.Application.Dtos.LoginPageImages;
using Crm.Application.Features.LoginPageImages.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.LoginPageImages.Queries;

/// <summary>
/// Handles the <see cref="GetPagedLoginPageImagesQuery"/> to retrieve all configured login page images.
/// </summary>
/// <param name="repository">The repository used to manage data access operations for login page images.</param>
/// <param name="fileUrlProvider">The service responsible for generating public URLs for the stored login page images.</param>
/// <param name="logger">The logger used to record the execution of the image retrieval process.</param>
public sealed partial class GetPagedLoginPageImagesQueryHandler(
    ILoginPageImageRepository repository,
    IFileUrlProvider fileUrlProvider,
    ILogger<GetPagedLoginPageImagesQueryHandler> logger) : IQueryHandler<GetPagedLoginPageImagesQuery, PagedResult<LoginPageImageDto>>
{
    public async ValueTask<PagedResult<LoginPageImageDto>> Handle(GetPagedLoginPageImagesQuery query, CancellationToken cancellationToken)
    {
        bool hasSearchFilter = !string.IsNullOrWhiteSpace(query.SearchTerm);
        LogFetchingImages(logger, query.PageNumber, query.PageSize, hasSearchFilter);

        var pagedResult = await repository.GetPagedAsync<LoginPageImageDto>(
            searchTerm: query.SearchTerm,
            orderBy: query.OrderBy,
            sortOrder: query.SortOrder,
            pageNumber: query.PageNumber,
            pageSize: query.PageSize,
            cancellationToken: cancellationToken);

        var updatedItems = pagedResult.Items
            .Select(item => item.WithResolvedUrl(fileUrlProvider))
            .ToList();

        LogImagesFetched(logger, pagedResult.Items.Count, pagedResult.TotalCount);

        return new PagedResult<LoginPageImageDto>(updatedItems, pagedResult.TotalCount, pagedResult.PageNumber, pagedResult.PageSize);
    }

    [LoggerMessage(EventId = LogEventIds.FetchingAllLoginPageImages, Level = LogLevel.Information, Message = "Fetching login page images - Page: {PageNumber}, Size: {PageSize}, HasSearchFilter: {HasSearchFilter}.")]
    private static partial void LogFetchingImages(ILogger logger, int pageNumber, int pageSize, bool hasSearchFilter);

    [LoggerMessage(EventId = LogEventIds.AllLoginPageImagesFetched, Level = LogLevel.Information, Message = "Successfully retrieved {FetchedCount} login page images out of {TotalCount} total matched.")]
    private static partial void LogImagesFetched(ILogger logger, int fetchedCount, int totalCount);
}
