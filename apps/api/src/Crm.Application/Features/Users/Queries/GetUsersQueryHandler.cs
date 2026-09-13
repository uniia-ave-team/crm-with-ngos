using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Queries;
using Crm.Application.Features.Users.Mappings;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Handles the <see cref="GetUsersQuery"/> to retrieve a paginated list of organizational users.
/// </summary>
public partial class GetUsersQueryHandler(
    IUserRepository userRepository,
    ILogger<GetUsersQueryHandler> logger) : IRequestHandler<GetUsersQuery, PagedResult<UserDto>>
{
    public async Task<PagedResult<UserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        LogFetchingUsers(logger, request.PageNumber, request.PageSize, request.SearchTerm);

        var filter = BuildUserFilter(request.SearchTerm, request.ShowDeleted);

        var pagedResult = await userRepository.GetPagedAsync(
            selector: UserMappingExtensions.ToDtoExpression(),
            predicate: filter,
            orderBy: request.OrderBy,
            sortOrder: request.SortOrder,
            pageNumber: request.PageNumber,
            pageSize: request.PageSize,
            ct: cancellationToken);

        LogUsersFetched(logger, pagedResult.Items.Count, pagedResult.TotalCount);

        return pagedResult;
    }

    /// <summary>
    /// Builds the filter expression for querying users based on search term and deletion status.
    /// </summary>
    [SuppressMessage("Globalization", "CA1311:Specify a culture or use an invariant version", Justification = "EF Core translates this to SQL")]
    [SuppressMessage("Globalization", "CA1307:Specify StringComparison for clarity", Justification = "EF Core translates this to SQL")]
    [SuppressMessage("Performance", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "EF Core requires ToLower for correct SQL translation")]
    private static Expression<Func<User, bool>> BuildUserFilter(string? searchTerm, bool showDeleted)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return u => showDeleted || u.IsActive;
        }

        string term = searchTerm.Trim().ToLowerInvariant();

        return u =>
            (showDeleted || u.IsActive) &&
            (
                u.FirstName.ToLower().Contains(term) ||
                u.LastName.ToLower().Contains(term) ||
                (u.Patronymic != null && u.Patronymic.ToLower().Contains(term)) ||
                (u.InternalPosition != null && u.InternalPosition.ToLower().Contains(term)) ||
                (u.PhoneNumber != null && u.PhoneNumber.ToLower().Contains(term)) ||
                (u.Country != null && u.Country.ToLower().Contains(term)) ||
                (u.AuthUser != null && u.AuthUser.Email != null && u.AuthUser.Email.ToLower().Contains(term)));
    }

    [LoggerMessage(EventId = LogEventIds.FetchingUsers, Level = LogLevel.Information, Message = "Fetching users - Page: {PageNumber}, Size: {PageSize}, Search: {SearchTerm}")]
    private static partial void LogFetchingUsers(ILogger logger, int pageNumber, int pageSize, string? searchTerm);

    [LoggerMessage(EventId = LogEventIds.UsersFetched, Level = LogLevel.Information, Message = "Successfully retrieved {FetchedCount} users out of {TotalCount} total matched.")]
    private static partial void LogUsersFetched(ILogger logger, int fetchedCount, int totalCount);
}
