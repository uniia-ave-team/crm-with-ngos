using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Users;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Command to generate a secure password restore token for a specified user.
/// </summary>
public record GenerateRestorePasswordTokenCommand(
    Guid UserId) : ICommand<TokenResultDto>, ITransactionalCommand;
