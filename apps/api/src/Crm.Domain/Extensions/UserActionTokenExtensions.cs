using Crm.Domain.Constants;
using Crm.Domain.Entities;

namespace Crm.Domain.Extensions;

/// <summary>
/// Provides centralized sort-field validation helpers for <see cref="UserActionToken"/> queries.
/// </summary>
public static class UserActionTokenExtensions
{
    /// <summary>
    /// The canonical API sort alias for sorting action tokens by target email address.
    /// </summary>
    public const string EmailOrderBy = "Email";

    /// <summary>
    /// Determines whether <paramref name="orderBy"/> is a valid sort field for active invitation lists.
    /// </summary>
    /// <param name="orderBy">The requested sort field name.</param>
    /// <returns><see langword="true"/> if supported; otherwise, <see langword="false"/>.</returns>
    public static bool IsAllowedInvitationOrderBy(string? orderBy)
        => orderBy is not null
            && orderBy.Trim().EqualsAnyIgnoreCase(
                EmailOrderBy,
                nameof(UserActionToken.TargetEmail),
                nameof(UserActionToken.CreatedAt),
                nameof(UserActionToken.ExpiresAt));

    /// <summary>
    /// Determines whether <paramref name="orderBy"/> is a valid sort field for active password reset token lists.
    /// </summary>
    /// <param name="orderBy">The requested sort field name.</param>
    /// <returns><see langword="true"/> if supported; otherwise, <see langword="false"/>.</returns>
    public static bool IsAllowedPasswordResetOrderBy(string? orderBy)
        => IsAllowedInvitationOrderBy(orderBy)
            || (orderBy is not null
                && orderBy.Trim().EqualsAnyIgnoreCase(
                    nameof(User.FirstName),
                    nameof(User.LastName),
                    UserConstants.FullName));
}
