using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Users;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Represents a command to atomically synchronize the global user custom field schema
/// and propagate additions, removals, key renames, and required backfills across all users' JSONB payloads.
/// </summary>
/// <param name="Fields">The complete target ordered list of user custom field definitions.</param>
public record SyncUserCustomFieldsCommand(
    IReadOnlyList<UpdateUserCustomFieldDto> Fields) : ICommand, ITransactionalCommand;
