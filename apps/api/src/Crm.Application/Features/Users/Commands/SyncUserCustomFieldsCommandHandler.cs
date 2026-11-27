using Crm.Application.Common.Consts;
using Crm.Application.Extensions;
using Crm.Domain.Entities;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles <see cref="SyncUserCustomFieldsCommand"/> to atomically reconcile global <see cref="UserCustomField"/> schema definitions
/// with immutable server-generated keys and synchronize user JSONB payloads when keys are added or removed.
/// </summary>
public sealed partial class SyncUserCustomFieldsCommandHandler(
    IUserCustomFieldRepository userCustomFieldRepository,
    IUserRepository userRepository,
    ILogger<SyncUserCustomFieldsCommandHandler> logger) : ICommandHandler<SyncUserCustomFieldsCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(SyncUserCustomFieldsCommand command, CancellationToken cancellationToken)
    {
        LogSyncingUserCustomFields(logger, command.Fields.Count);

        var existingFields = await userCustomFieldRepository.GetAllForUpdateAsync(cancellationToken);

        var plan = existingFields.SynchronizeFields(command.Fields);

        existingFields.EnsureValidFieldSchema(allowEmpty: true);

        await userCustomFieldRepository.ApplySchemaChangesAsync(
            addedFields: plan.AddedFields,
            removedFields: plan.RemovedFields,
            cancellationToken: cancellationToken);

        if (plan.RequiresJsonbSync)
        {
            await userRepository.SyncCustomFieldsSchemaAsync(plan, cancellationToken);
        }

        LogUserCustomFieldsSyncedSuccessfully(logger, existingFields.Count);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.SyncingUserCustomFields, Level = LogLevel.Information, Message = "Synchronizing global user custom fields schema with {FieldCount} incoming field definition(s)")]
    private static partial void LogSyncingUserCustomFields(ILogger logger, int fieldCount);

    [LoggerMessage(EventId = LogEventIds.UserCustomFieldsSyncedSuccessfully, Level = LogLevel.Information, Message = "Successfully synchronized global user custom fields schema ({FieldCount} active field(s))")]
    private static partial void LogUserCustomFieldsSyncedSuccessfully(ILogger logger, int fieldCount);
}
