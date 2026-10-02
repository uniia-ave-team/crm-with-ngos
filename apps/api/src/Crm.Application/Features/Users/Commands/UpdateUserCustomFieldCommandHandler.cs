using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User.Commands;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="UpdateUserCustomFieldCommand"/> to update an existing custom field's value.
/// Ensures that users can only update their own custom fields.
/// </summary>
public partial class UpdateUserCustomFieldCommandHandler(
    IUserCustomFieldRepository customFieldRepository,
    ILogger<UpdateUserCustomFieldCommandHandler> logger) : IRequestHandler<UpdateUserCustomFieldCommand>
{
    public async Task Handle(UpdateUserCustomFieldCommand request, CancellationToken cancellationToken)
    {
        LogUpdatingCustomField(logger, request.CustomFieldId, request.UserId);

        var customField = await customFieldRepository.GetForUpdateAsync(request.CustomFieldId, cancellationToken);

        if (customField.UserId != request.UserId)
        {
            LogUnauthorizedCustomFieldUpdate(logger, request.CustomFieldId, request.UserId);
            throw new InvalidOperationException("Cannot update a custom field that belongs to another user.");
        }

        request.Adapt(customField);

        LogCustomFieldUpdatedSuccessfully(logger, request.CustomFieldId, request.UserId);
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingUserCustomField, Level = LogLevel.Information, Message = "Updating custom field ID: {CustomFieldId} for user ID: {UserId}")]
    private static partial void LogUpdatingCustomField(ILogger logger, Guid customFieldId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserCustomFieldUpdatedSuccessfully, Level = LogLevel.Information, Message = "Successfully updated custom field ID: {CustomFieldId} for user ID: {UserId}")]
    private static partial void LogCustomFieldUpdatedSuccessfully(ILogger logger, Guid customFieldId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UnauthorizedCustomFieldUpdate, Level = LogLevel.Warning, Message = "User ID: {UserId} attempted to modify custom field ID: {CustomFieldId} which belongs to a different user.")]
    private static partial void LogUnauthorizedCustomFieldUpdate(ILogger logger, Guid customFieldId, Guid userId);
}
