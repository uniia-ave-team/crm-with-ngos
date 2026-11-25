using Crm.Application.Dtos.Users;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;

namespace Crm.Application.Features.Users.Extensions;

/// <summary>
/// Provides shared validation and resolution helpers for invitation and password reset action tokens.
/// </summary>
internal static class ActionTokenExtensions
{
    /// <summary>
    /// Resolves an active invitation token and validates that its decrypted metadata contains
    /// a valid NGO identifier and at least one assigned role identifier.
    /// </summary>
    /// <param name="actionTokenService">The action token service.</param>
    /// <param name="rawToken">The raw opaque invitation token from the request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The validated <see cref="ResolvedActionToken{InvitationTokenMetadata}"/>.</returns>
    /// <exception cref="DomainValidationException">Thrown when the token is missing, expired, or has invalid metadata.</exception>
    public static async Task<ResolvedActionToken<InvitationTokenMetadata>> ResolveValidInvitationAsync(
        this IUserActionTokenService actionTokenService,
        string rawToken,
        CancellationToken cancellationToken)
    {
        var resolved = await actionTokenService.ResolveActiveAsync<InvitationTokenMetadata>(
            rawToken,
            ActionTokenType.Invitation,
            cancellationToken);

        return resolved is not null
            && resolved.Metadata.NgoId != Guid.Empty
            && resolved.Metadata.RoleIds is { Count: > 0 }
                ? resolved
                : throw new DomainValidationException("Invalid or expired invitation token.");
    }

    /// <summary>
    /// Resolves an active password reset token and validates that it targets an existing user
    /// and carries a non-empty underlying ASP.NET Identity reset token.
    /// </summary>
    /// <param name="actionTokenService">The action token service.</param>
    /// <param name="rawToken">The raw opaque password reset token from the request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// The validated <see cref="ResolvedActionToken{PasswordResetTokenMetadata}"/>,
    /// or <see langword="null"/> if the token is missing, expired, revoked, or malformed.
    /// </returns>
    public static async Task<ResolvedActionToken<PasswordResetTokenMetadata>?> ResolveValidPasswordResetAsync(
        this IUserActionTokenService actionTokenService,
        string rawToken,
        CancellationToken cancellationToken)
    {
        var resolved = await actionTokenService.ResolveActiveAsync<PasswordResetTokenMetadata>(
            rawToken,
            ActionTokenType.PasswordReset,
            cancellationToken);

        return resolved is not null
            && resolved.Token.TargetUserId.HasValue
            && !string.IsNullOrWhiteSpace(resolved.Metadata.IdentityResetToken)
                ? resolved
                : null;
    }
}
