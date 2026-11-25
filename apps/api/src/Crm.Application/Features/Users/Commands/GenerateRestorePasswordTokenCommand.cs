using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Users;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Command to generate a secure password restore token for a specified user.
/// </summary>
/// <param name="UserId">The unique identifier of the user whose password will be reset.</param>
public record GenerateRestorePasswordTokenCommand(
    Guid UserId) : ICommand<TokenResultDto>, ITransactionalCommand, ICurrentUserAware
{
    /// <inheritdoc />
    [JsonIgnore]
    public Guid CurrentUserId { get; set; }
}
