using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Users;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Handles <see cref="GetUserCustomFieldsQuery"/> to return ordered global user custom field definitions
/// visible to the requesting user.
/// </summary>
public sealed partial class GetUserCustomFieldsQueryHandler(
    IUserCustomFieldRepository userCustomFieldRepository,
    IPermissionService permissionService,
    ILogger<GetUserCustomFieldsQueryHandler> logger)
    : IQueryHandler<GetUserCustomFieldsQuery, IReadOnlyList<UserCustomFieldDto>>
{
    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<UserCustomFieldDto>> Handle(
        GetUserCustomFieldsQuery query,
        CancellationToken cancellationToken)
    {
        LogFetchingUserCustomFields(logger, query.IncludePrivate);

        var fields = await userCustomFieldRepository.GetAllOrderedAsync(cancellationToken);

        bool canViewPrivate = query.IncludePrivate
            || await permissionService.HasAccessAsync(
                query.CurrentUserRoleIds,
                AccessRight.ViewCustomFields,
                cancellationToken);

        IReadOnlyList<Domain.Entities.UserCustomField> visibleFields = canViewPrivate
            ? fields
            : [.. fields.Where(f => f.IsPublic)];

        var result = visibleFields.Adapt<List<UserCustomFieldDto>>();

        LogUserCustomFieldsFetched(logger, result.Count);

        return result;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingUserCustomFields, Level = LogLevel.Information, Message = "Fetching global user custom field definitions (IncludePrivate: {IncludePrivate})")]
    private static partial void LogFetchingUserCustomFields(ILogger logger, bool includePrivate);

    [LoggerMessage(EventId = LogEventIds.UserCustomFieldsFetched, Level = LogLevel.Information, Message = "Successfully retrieved {Count} visible user custom field definition(s)")]
    private static partial void LogUserCustomFieldsFetched(ILogger logger, int count);
}
