using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Users;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Enums;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Handles the <see cref="GetActiveInvitationsQuery"/> to retrieve a paginated, filtered, and sorted list
/// of active (unexpired) user invitations.
/// </summary>
/// <param name="actionTokenService">The service providing active invitation tokens with decrypted metadata.</param>
/// <param name="logger">The logger used to record query execution.</param>
public sealed partial class GetActiveInvitationsQueryHandler(
    IUserActionTokenService actionTokenService,
    ILogger<GetActiveInvitationsQueryHandler> logger)
    : IQueryHandler<GetActiveInvitationsQuery, PagedResult<ActiveInvitationDto>>
{
    /// <inheritdoc />
    public async ValueTask<PagedResult<ActiveInvitationDto>> Handle(
        GetActiveInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        LogFetchingActiveInvitations(logger, query.PageNumber, query.PageSize);

        PagedResult<ResolvedActionToken<InvitationTokenMetadata>> page =
            await actionTokenService.GetPagedActiveWithMetadataAsync<InvitationTokenMetadata>(
                ActionTokenType.Invitation,
                query.ToFilter(),
                cancellationToken);

        var items = page.Items
            .Select(r => new ActiveInvitationDto(
                r.Token.Id,
                r.Token.TargetEmail,
                r.Metadata.RoleIds,
                r.Token.CreatedAt,
                r.Token.ExpiresAt,
                r.Token.CreatedBy))
            .ToList();

        LogActiveInvitationsFetched(logger, items.Count, page.TotalCount);

        return new PagedResult<ActiveInvitationDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }

    [LoggerMessage(EventId = LogEventIds.FetchingActiveInvitations, Level = LogLevel.Information, Message = "Fetching active invitation tokens - Page: {PageNumber}, Size: {PageSize}.")]
    private static partial void LogFetchingActiveInvitations(ILogger logger, int pageNumber, int pageSize);

    [LoggerMessage(EventId = LogEventIds.ActiveInvitationsFetched, Level = LogLevel.Information, Message = "Fetched {Count} active invitation tokens out of {TotalCount} total matched.")]
    private static partial void LogActiveInvitationsFetched(ILogger logger, int count, int totalCount);
}
