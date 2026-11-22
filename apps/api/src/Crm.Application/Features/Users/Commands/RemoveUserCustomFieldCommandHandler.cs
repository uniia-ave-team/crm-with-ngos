using Crm.Application.Common.Consts;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="RemoveUserCustomFieldCommand"/> to permanently delete a specific custom field from a user profile.
/// Ensures that users can only remove their own custom fields.
/// </summary>
public sealed partial class RemoveUserCustomFieldCommandHandler(
    IUserCustomFieldRepository customFieldRepository,
    ILogger<RemoveUserCustomFieldCommandHandler> logger) : ICommandHandler<RemoveUserCustomFieldCommand>
{
    public async ValueTask<Unit> Handle(RemoveUserCustomFieldCommand command, CancellationToken cancellationToken)
    {
        LogRemovingCustomField(logger, command.CustomFieldId, command.UserId);

        var customField = await customFieldRepository.GetAsync(command.CustomFieldId, cancellationToken);

        if (customField.UserId != command.UserId)
        {
            LogUnauthorizedCustomFieldRemoval(logger, command.CustomFieldId, command.UserId);
            throw new UnauthorizedAccessException("Cannot remove a custom field that belongs to another user.");
        }

        await customFieldRepository.DeleteAsync(command.CustomFieldId, cancellationToken);

        LogCustomFieldRemovedSuccessfully(logger, command.CustomFieldId, command.UserId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.RemovingUserCustomField, Level = LogLevel.Information, Message = "Removing custom field ID: {CustomFieldId} for user ID: {UserId}")]
    private static partial void LogRemovingCustomField(ILogger logger, Guid customFieldId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserCustomFieldRemovedSuccessfully, Level = LogLevel.Information, Message = "Successfully removed custom field ID: {CustomFieldId} for user ID: {UserId}")]
    private static partial void LogCustomFieldRemovedSuccessfully(ILogger logger, Guid customFieldId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UnauthorizedCustomFieldRemoval, Level = LogLevel.Warning, Message = "User ID: {UserId} attempted to remove custom field ID: {CustomFieldId} which belongs to a different user.")]
    private static partial void LogUnauthorizedCustomFieldRemoval(ILogger logger, Guid customFieldId, Guid userId);
}
