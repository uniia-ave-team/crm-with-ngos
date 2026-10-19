using MediatR;

namespace Crm.Application.Dtos.User.Commands;

/// <summary>
/// Command to generate a secure password restore token for a specified user.
/// </summary>
public record GenerateRestorePasswordTokenCommand(
    Guid UserId) : IRequest<TokenResultDto>;
