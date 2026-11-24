using Crm.Domain.Entities;
using Crm.Domain.Exceptions;

namespace Crm.Domain.Extensions;

/// <summary>
/// Provides domain behavior extension methods for the <see cref="MaintenanceNotice"/> entity.
/// </summary>
public static class MaintenanceNoticeExtensions
{
    /// <summary>
    /// Updates the maintenance notice message and schedule in UTC+0 while enforcing domain invariants.
    /// </summary>
    /// <param name="notice">The maintenance notice entity to update.</param>
    /// <param name="message">The user-facing announcement message.</param>
    /// <param name="startsAt">The timestamp when the maintenance window starts (normalized to UTC+0).</param>
    /// <param name="endsAt">The timestamp when the maintenance window ends (normalized to UTC+0).</param>
    /// <param name="updatedAt">The timestamp of this update (normalized to UTC+0).</param>
    /// <returns>The updated <see cref="MaintenanceNotice"/> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="notice"/> is <c>null</c>.</exception>
    /// <exception cref="DomainValidationException">Thrown when <paramref name="message"/> is null or whitespace.</exception>
    /// <exception cref="InvalidMaintenanceWindowException">Thrown when <paramref name="endsAt"/> is not strictly after <paramref name="startsAt"/>.</exception>
    public static MaintenanceNotice Update(
        this MaintenanceNotice notice,
        string message,
        DateTime startsAt,
        DateTime endsAt,
        DateTime updatedAt)
    {
        ArgumentNullException.ThrowIfNull(notice);

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new DomainValidationException("Maintenance notice message cannot be empty.");
        }

        DateTime startsAtUtc = startsAt.ToUtc();
        DateTime endsAtUtc = endsAt.ToUtc();
        DateTime updatedAtUtc = updatedAt.ToUtc();

        if (endsAtUtc <= startsAtUtc)
        {
            throw new InvalidMaintenanceWindowException();
        }

        notice.Message = message.Trim();
        notice.StartsAt = startsAtUtc;
        notice.EndsAt = endsAtUtc;
        notice.UpdatedAt = updatedAtUtc;

        return notice;
    }
}
