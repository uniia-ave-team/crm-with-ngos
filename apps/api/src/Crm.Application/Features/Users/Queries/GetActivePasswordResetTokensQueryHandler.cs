using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Users;
using Crm.Application.Features.Users.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Enums;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Handles the <see cref="GetActivePasswordResetTokensQuery"/> to retrieve a paginated, filtered, and sorted list
/// of active (unexpired) password reset tokens with target user avatar URLs.
/// </summary>
/// <param name="actionTokenService">The service providing paginated access to active action tokens.</param>
/// <param name="fileUrlProvider">The service used to resolve target users' avatar URLs.</param>
/// <param name="logger">The logger used to record query execution.</param>
public sealed partial class GetActivePasswordResetTokensQueryHandler(
    IUserActionTokenService actionTokenService,
    IFileUrlProvider fileUrlProvider,
    ILogger<GetActivePasswordResetTokensQueryHandler> logger)
    : IQueryHandler<GetActivePasswordResetTokensQuery, PagedResult<ActivePasswordResetTokenDto>>
{
    /// <inheritdoc />
    public async ValueTask<PagedResult<ActivePasswordResetTokenDto>> Handle(
        GetActivePasswordResetTokensQuery query,
        CancellationToken cancellationToken)
    {
        LogFetchingActivePasswordResetTokens(logger, query.PageNumber, query.PageSize, query.UserId);

        PagedResult<ActivePasswordResetTokenDto> page =
            await actionTokenService.GetPagedActiveAsync<ActivePasswordResetTokenDto>(
                ActionTokenType.PasswordReset,
                query.ToFilter(),
                cancellationToken);

        var items = page.Items
            .Select(item => item with
            {
                AvatarUrl = fileUrlProvider.ResolveUserAvatarUrl(item.AvatarUrl, item.UserId),
            })
            .ToList();

        LogActivePasswordResetTokensFetched(logger, items.Count, page.TotalCount);

        return new PagedResult<ActivePasswordResetTokenDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }

    [LoggerMessage(EventId = LogEventIds.FetchingActivePasswordResetTokens, Level = LogLevel.Information, Message = "Fetching active password reset tokens - Page: {PageNumber}, Size: {PageSize}, UserId: {UserId}.")]
    private static partial void LogFetchingActivePasswordResetTokens(ILogger logger, int pageNumber, int pageSize, Guid? userId);

    [LoggerMessage(EventId = LogEventIds.ActivePasswordResetTokensFetched, Level = LogLevel.Information, Message = "Fetched {Count} active password reset tokens out of {TotalCount} total matched.")]
    private static partial void LogActivePasswordResetTokensFetched(ILogger logger, int count, int totalCount);
}
