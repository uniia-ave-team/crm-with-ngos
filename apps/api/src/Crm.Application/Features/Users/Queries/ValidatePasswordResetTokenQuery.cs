using Crm.Application.Dtos.Users;
using Mediator;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Query to validate a password reset token and retrieve target account details so the user can confirm whose password is being reset.
/// </summary>
/// <param name="Token">The opaque password reset token string from the reset link.</param>
public record ValidatePasswordResetTokenQuery(string Token) : IQuery<PasswordResetDetailsDto>;
