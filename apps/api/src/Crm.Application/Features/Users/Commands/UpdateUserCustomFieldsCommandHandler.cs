using Crm.Application.Common.Consts;
using Crm.Application.Features.Users.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles <see cref="UpdateUserCustomFieldsCommand"/> to validate a user's JSON payload against the editable
/// <see cref="Domain.Entities.UserCustomField"/> schema, preserve protected private fields for non-privileged editors,
/// and persist the aligned payload.
/// </summary>
public sealed partial class UpdateUserCustomFieldsCommandHandler(
    IUserRepository userRepository,
    IUserCustomFieldRepository userCustomFieldRepository,
    IPermissionService permissionService,
    ILogger<UpdateUserCustomFieldsCommandHandler> logger) : ICommandHandler<UpdateUserCustomFieldsCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(UpdateUserCustomFieldsCommand command, CancellationToken cancellationToken)
    {
        LogUpdatingUserCustomFields(logger, command.UserId);

        var user = await userRepository.GetForUpdateAsync(command.UserId, cancellationToken);
        var schemaFields = await userCustomFieldRepository.GetAllOrderedAsync(cancellationToken);

        bool isSelf = command.TargetCurrentUser || command.CurrentUserId == command.UserId;
        bool hasPrivateFields = schemaFields.Any(f => !f.IsPublic);
        bool canEditPrivate = isSelf
            || !hasPrivateFields
            || await permissionService.HasAccessAsync(
                command.CurrentUserRoleIds,
                AccessRight.ViewCustomFields,
                cancellationToken);

        user.CustomFields = command.Data.ValidateAndMergeCustomFields(
            user.CustomFields,
            schemaFields,
            canEditPrivate);

        LogUserCustomFieldsUpdatedSuccessfully(logger, user.Id);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingUserCustomField, Level = LogLevel.Information, Message = "Initiating custom fields update for user ID: {UserId}")]
    private static partial void LogUpdatingUserCustomFields(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserCustomFieldUpdatedSuccessfully, Level = LogLevel.Information, Message = "Successfully updated custom fields for user ID: {UserId}")]
    private static partial void LogUserCustomFieldsUpdatedSuccessfully(ILogger logger, Guid userId);
}
