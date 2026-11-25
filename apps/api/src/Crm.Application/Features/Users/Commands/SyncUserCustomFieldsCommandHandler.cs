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
/// and apply matching JSONB payload patches across all users.
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

        var keysChangingType = existingFields.FindKeysWithDataTypeChange(command.Fields);
        HashSet<string> keysWithValues = keysChangingType.Count > 0
            ? await userRepository.GetKeysWithNonNullCustomFieldValuesAsync(keysChangingType, cancellationToken)
            : [];

        var diff = existingFields.ComputeSchemaDiff(command.Fields, existing => keysWithValues.Contains(existing.Key));

        existingFields.EnsureValidFieldSchema(allowEmpty: true);

        await userCustomFieldRepository.ApplySchemaChangesAsync(
            addedFields: diff.FieldsToAdd.Cast<UserCustomField>(),
            removedFields: diff.FieldsToRemove.Cast<UserCustomField>(),
            cancellationToken: cancellationToken);

        await userCustomFieldRepository.ResolveFieldKeyCollisionsAsync(cancellationToken);

        await userRepository.SyncCustomFieldsSchemaAsync(diff, cancellationToken);

        LogUserCustomFieldsSyncedSuccessfully(logger, existingFields.Count);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.SyncingUserCustomFields, Level = LogLevel.Information, Message = "Synchronizing global user custom fields schema with {FieldCount} incoming field definition(s)")]
    private static partial void LogSyncingUserCustomFields(ILogger logger, int fieldCount);

    [LoggerMessage(EventId = LogEventIds.UserCustomFieldsSyncedSuccessfully, Level = LogLevel.Information, Message = "Successfully synchronized global user custom fields schema ({FieldCount} active field(s))")]
    private static partial void LogUserCustomFieldsSyncedSuccessfully(ILogger logger, int fieldCount);
}
