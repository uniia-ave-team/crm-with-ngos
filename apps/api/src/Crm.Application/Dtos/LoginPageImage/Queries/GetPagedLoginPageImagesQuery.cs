using Crm.Domain.Common;
using Crm.Domain.Consts;
using MediatR;

namespace Crm.Application.Dtos.LoginPageImage.Queries;

/// <summary>
/// Represents a query to retrieve a paginated, filtered, and sorted list of login page images.
/// Implements the MediatR query pattern returning a paginated collection of login page image data transfer objects.
/// </summary>
/// <param name="SearchTerm">
/// An optional search term to filter images by their URL.
/// If null or whitespace, no text filtering is applied.
/// </param>
/// <param name="OrderBy">
/// The name of the entity property by which the collection should be sorted (e.g., "ImageUrl").
/// </param>
/// <param name="SortOrder">
/// The direction of sorting. Use <see cref="SortOrderConstants.Ascending"/> ("asc")
/// or <see cref="SortOrderConstants.Descending"/> ("desc"). Default is ascending.
/// </param>
/// <param name="PageNumber">
/// The target page number to retrieve (1-based index). Default is 1.
/// </param>
/// <param name="PageSize">
/// The maximum number of items to include per page. Default is 10.
/// </param>
public record GetPagedLoginPageImagesQuery(
    string? SearchTerm = null,
    string? OrderBy = nameof(LoginPageImageDto.Url),
    string? SortOrder = SortOrderConstants.Ascending,
    int PageNumber = PaginationConstants.MinPageNumber,
    int PageSize = PaginationConstants.DefaultPageSize) : IRequest<PagedResult<LoginPageImageDto>>;
