using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User.Commands;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="AddUserCustomFieldCommand"/> to create and attach a new custom field to a user profile.
/// </summary>
public partial class AddUserCustomFieldCommandHandler(
    IUserCustomFieldRepository customFieldRepository,
    IUserRepository userRepository,
    ILogger<AddUserCustomFieldCommandHandler> logger) : IRequestHandler<AddUserCustomFieldCommand>
{
    public async ValueTask<Unit> Handle(AddUserCustomFieldCommand request, CancellationToken cancellationToken)
    {
        // TODO: Make another handler to add a custom field to many users at once (or introduce a global user schema / field definition) and implement bulk delete for that as well.
        LogAddingCustomField(logger, request.Key, request.UserId);

        await userRepository.EnsureExistsAsync(request.UserId, cancellationToken);

        await customFieldRepository.EnsureKeyDoesNotExistAsync(request.UserId, request.Key, cancellationToken);

        await customFieldRepository.CreateAsync(request.Adapt<UserCustomField>(), cancellationToken);

        LogCustomFieldAddedSuccessfully(logger, request.Key, request.UserId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.AddingUserCustomField, Level = LogLevel.Information, Message = "Adding custom field '{Key}' for user ID: {UserId}")]
    private static partial void LogAddingCustomField(ILogger logger, string key, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserCustomFieldAddedSuccessfully, Level = LogLevel.Information, Message = "Successfully added custom field '{Key}' for user ID: {UserId}")]
    private static partial void LogCustomFieldAddedSuccessfully(ILogger logger, string key, Guid userId);
}
