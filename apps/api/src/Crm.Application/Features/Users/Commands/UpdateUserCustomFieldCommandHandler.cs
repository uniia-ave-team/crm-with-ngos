using Crm.Application.Common.Consts;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="UpdateUserCustomFieldCommand"/> to update an existing custom field's value.
/// Ensures that users can only update their own custom fields.
/// </summary>
public sealed partial class UpdateUserCustomFieldCommandHandler(
    IUserCustomFieldRepository customFieldRepository,
    ILogger<UpdateUserCustomFieldCommandHandler> logger) : ICommandHandler<UpdateUserCustomFieldCommand>
{
    public async ValueTask<Unit> Handle(UpdateUserCustomFieldCommand command, CancellationToken cancellationToken)
    {
        LogUpdatingCustomField(logger, command.CustomFieldId, command.UserId);

        var customField = await customFieldRepository.GetForUpdateAsync(command.CustomFieldId, cancellationToken);

        if (customField.UserId != command.UserId)
        {
            LogUnauthorizedCustomFieldUpdate(logger, command.CustomFieldId, command.UserId);
            throw new UnauthorizedAccessException("Cannot update a custom field that belongs to another user.");
        }

        command.Adapt(customField);

        LogCustomFieldUpdatedSuccessfully(logger, command.CustomFieldId, command.UserId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingUserCustomField, Level = LogLevel.Information, Message = "Updating custom field ID: {CustomFieldId} for user ID: {UserId}")]
    private static partial void LogUpdatingCustomField(ILogger logger, Guid customFieldId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserCustomFieldUpdatedSuccessfully, Level = LogLevel.Information, Message = "Successfully updated custom field ID: {CustomFieldId} for user ID: {UserId}")]
    private static partial void LogCustomFieldUpdatedSuccessfully(ILogger logger, Guid customFieldId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UnauthorizedCustomFieldUpdate, Level = LogLevel.Warning, Message = "User ID: {UserId} attempted to modify custom field ID: {CustomFieldId} which belongs to a different user.")]
    private static partial void LogUnauthorizedCustomFieldUpdate(ILogger logger, Guid customFieldId, Guid userId);
}
