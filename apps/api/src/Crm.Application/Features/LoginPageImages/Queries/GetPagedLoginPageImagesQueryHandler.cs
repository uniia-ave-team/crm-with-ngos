using Crm.Application.Common.Consts;
using Crm.Application.Dtos.LoginPageImage;
using Crm.Application.Dtos.LoginPageImage.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.LoginPageImages.Queries;

/// <summary>
/// Handles the <see cref="GetPagedLoginPageImagesQuery"/> to retrieve all configured login page images.
/// </summary>
/// <param name="repository">The repository used to manage data access operations for login page images.</param>
/// <param name="fileUrlProvider">The service responsible for generating public URLs for the stored login page images.</param>
/// <param name="logger">The logger used to record the execution of the image retrieval process.</param>
public partial class GetPagedLoginPageImagesQueryHandler(
    ILoginPageImageRepository repository,
    IFileUrlProvider fileUrlProvider,
    ILogger<GetPagedLoginPageImagesQueryHandler> logger) : IRequestHandler<GetPagedLoginPageImagesQuery, PagedResult<LoginPageImageDto>>
{
    public async Task<PagedResult<LoginPageImageDto>> Handle(GetPagedLoginPageImagesQuery request, CancellationToken cancellationToken)
    {
        LogFetchingImages(logger, request.PageNumber, request.PageSize, request.SearchTerm);

        var pagedResult = await repository.GetPagedAsync<LoginPageImageDto>(
            searchTerm: request.SearchTerm,
            orderBy: request.OrderBy,
            sortOrder: request.SortOrder,
            pageNumber: request.PageNumber,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        var updatedItems = pagedResult.Items
            .Select(item => item with
            {
                Url = fileUrlProvider.GetFileUrl(item.Url, ApiRouteLogoConstants.LoginPageImageFile(item.Id)) ?? item.Url,
            })
            .ToList();

        LogImagesFetched(logger, pagedResult.Items.Count, pagedResult.TotalCount);

        return new PagedResult<LoginPageImageDto>(updatedItems, pagedResult.TotalCount, pagedResult.PageNumber, pagedResult.PageSize);
    }

    [LoggerMessage(EventId = LogEventIds.FetchingAllLoginPageImages, Level = LogLevel.Information, Message = "Fetching login page images - Page: {pageNumber}, Size: {pageSize}, Search: {searchTerm}.")]
    private static partial void LogFetchingImages(ILogger logger, int pageNumber, int pageSize, string? searchTerm);

    [LoggerMessage(EventId = LogEventIds.AllLoginPageImagesFetched, Level = LogLevel.Information, Message = "Successfully retrieved {fetchedCount} login page images out of {totalCount} total matched.")]
    private static partial void LogImagesFetched(ILogger logger, int fetchedCount, int totalCount);
}
