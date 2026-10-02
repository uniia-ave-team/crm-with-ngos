using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User.Commands;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="RemoveUserCustomFieldCommand"/> to permanently delete a specific custom field from a user profile.
/// Ensures that users can only remove their own custom fields.
/// </summary>
public partial class RemoveUserCustomFieldCommandHandler(
    IUserCustomFieldRepository customFieldRepository,
    ILogger<RemoveUserCustomFieldCommandHandler> logger) : IRequestHandler<RemoveUserCustomFieldCommand>
{
    public async Task Handle(RemoveUserCustomFieldCommand request, CancellationToken cancellationToken)
    {
        LogRemovingCustomField(logger, request.CustomFieldId, request.UserId);

        var customField = await customFieldRepository.GetAsync(request.CustomFieldId, cancellationToken);

        if (customField.UserId != request.UserId)
        {
            LogUnauthorizedCustomFieldRemoval(logger, request.CustomFieldId, request.UserId);
            throw new InvalidOperationException("Cannot remove a custom field that belongs to another user.");
        }

        await customFieldRepository.DeleteAsync(request.CustomFieldId, cancellationToken);

        LogCustomFieldRemovedSuccessfully(logger, request.CustomFieldId, request.UserId);
    }

    [LoggerMessage(EventId = LogEventIds.RemovingUserCustomField, Level = LogLevel.Information, Message = "Removing custom field ID: {CustomFieldId} for user ID: {UserId}")]
    private static partial void LogRemovingCustomField(ILogger logger, Guid customFieldId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserCustomFieldRemovedSuccessfully, Level = LogLevel.Information, Message = "Successfully removed custom field ID: {CustomFieldId} for user ID: {UserId}")]
    private static partial void LogCustomFieldRemovedSuccessfully(ILogger logger, Guid customFieldId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UnauthorizedCustomFieldRemoval, Level = LogLevel.Warning, Message = "User ID: {UserId} attempted to remove custom field ID: {CustomFieldId} which belongs to a different user.")]
    private static partial void LogUnauthorizedCustomFieldRemoval(ILogger logger, Guid customFieldId, Guid userId);
}
