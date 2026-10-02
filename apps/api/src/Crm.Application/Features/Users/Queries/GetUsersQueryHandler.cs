using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Queries;
using Crm.Domain.Common;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Handles the <see cref="GetUsersQuery"/> to retrieve a paginated list of organizational users.
/// </summary>
public partial class GetUsersQueryHandler(
    IAuthUserRepository userRepository,
    ILogger<GetUsersQueryHandler> logger) : IRequestHandler<GetUsersQuery, PagedResult<UserDto>>
{
    public async Task<PagedResult<UserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        LogFetchingUsers(logger, request.PageNumber, request.PageSize, request.SearchTerm);

        var pagedResult = await userRepository.GetPagedAsync<UserDto>(
            searchTerm: request.SearchTerm,
            orderBy: request.OrderBy,
            sortOrder: request.SortOrder,
            pageNumber: request.PageNumber,
            pageSize: request.PageSize,
            showDeleted: request.ShowDeleted,
            cancellationToken: cancellationToken);

        LogUsersFetched(logger, pagedResult.Items.Count, pagedResult.TotalCount);

        return pagedResult;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingUsers, Level = LogLevel.Information, Message = "Fetching users - Page: {PageNumber}, Size: {PageSize}, Search: {SearchTerm}")]
    private static partial void LogFetchingUsers(ILogger logger, int pageNumber, int pageSize, string? searchTerm);

    [LoggerMessage(EventId = LogEventIds.UsersFetched, Level = LogLevel.Information, Message = "Successfully retrieved {FetchedCount} users out of {TotalCount} total matched.")]
    private static partial void LogUsersFetched(ILogger logger, int fetchedCount, int totalCount);
}
