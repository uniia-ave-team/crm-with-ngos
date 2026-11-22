using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Domain.ValueObjects;

namespace Crm.Domain.Extensions;

/// <summary>
/// Provides domain behavior extension methods for the <see cref="ProjectTask"/> entity,
/// encapsulating external-link updates and the 5-status lifecycle with UTC+0 date invariants.
/// </summary>
public static class ProjectTaskExtensions
{
    /// <summary>
    /// Replaces the task's external links collection in-place while preserving EF Core change tracking.
    /// </summary>
    /// <param name="task">The project task entity to update.</param>
    /// <param name="links">The new set of external links to attach to the task.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="task"/> or <paramref name="links"/> is <c>null</c>.</exception>
    public static void ReplaceExternalLinks(this ProjectTask task, IEnumerable<TaskExternalLink> links)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(links);

        task.ExternalLinks.Clear();
        foreach (var link in links)
        {
            task.ExternalLinks.Add(link);
        }
    }

    /// <summary>
    /// Applies a full status and end-date replacement (creation or full update semantics),
    /// clearing <see cref="ProjectTask.EndDate"/> when <paramref name="requestedEndDate"/> is <c>null</c> and
    /// <paramref name="newStatus"/> is not <see cref="ProjectTaskStatus.Completed"/>.
    /// </summary>
    /// <param name="task">The project task entity to update.</param>
    /// <param name="newStatus">The target execution status.</param>
    /// <param name="requestedEndDate">An optional explicit end date (normalized to UTC+0).</param>
    /// <param name="utcNow">The current UTC timestamp (normalized to UTC+0).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="task"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="newStatus"/> is not a defined <see cref="ProjectTaskStatus"/> value.</exception>
    /// <exception cref="InvalidDateRangeException">Thrown when the resulting end date is earlier than <see cref="ProjectTask.StartDate"/>.</exception>
    public static void ApplyFullStatusUpdate(
        this ProjectTask task,
        ProjectTaskStatus newStatus,
        DateTime? requestedEndDate,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(task);
        EnsureDefinedStatus(newStatus);

        task.StartDate = task.StartDate.ToUtc();
        task.EndDate = task.EndDate.ToUtc();
        DateTime nowUtc = utcNow.ToUtc();
        DateTime? requestedEndUtc = requestedEndDate.ToUtc();

        if (requestedEndUtc is { } explicitEndDate)
        {
            ApplyExplicitEndDate(task, newStatus, explicitEndDate);
        }
        else if (newStatus == ProjectTaskStatus.Completed)
        {
            bool alreadyCompletedWithDate = task.Status == ProjectTaskStatus.Completed && task.EndDate is not null;
            if (!alreadyCompletedWithDate)
            {
                EnsureValidEndDate(task, nowUtc);
                task.EndDate = nowUtc;
                task.EndDateWasAuto = true;
            }
            else
            {
                EnsureValidEndDate(task, task.EndDate!.Value);
            }
        }
        else
        {
            task.EndDate = null;
            task.EndDateWasAuto = false;
        }

        task.Status = newStatus;
        task.UpdatedAt = nowUtc;
    }

    /// <summary>
    /// Transitions the task status (partial update semantics), preserving manually specified end dates
    /// when <paramref name="requestedEndDate"/> is <c>null</c>, auto-populating <see cref="ProjectTask.EndDate"/>
    /// when completing without one, and clearing auto-populated end dates on reopen.
    /// </summary>
    /// <param name="task">The project task entity to transition.</param>
    /// <param name="newStatus">The target execution status.</param>
    /// <param name="requestedEndDate">An optional explicit end date (normalized to UTC+0).</param>
    /// <param name="utcNow">The current UTC timestamp (normalized to UTC+0).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="task"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="newStatus"/> is not a defined <see cref="ProjectTaskStatus"/> value.</exception>
    /// <exception cref="InvalidDateRangeException">Thrown when the resulting end date is earlier than <see cref="ProjectTask.StartDate"/>.</exception>
    public static void TransitionStatus(
        this ProjectTask task,
        ProjectTaskStatus newStatus,
        DateTime? requestedEndDate,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(task);
        EnsureDefinedStatus(newStatus);

        task.StartDate = task.StartDate.ToUtc();
        task.EndDate = task.EndDate.ToUtc();
        DateTime nowUtc = utcNow.ToUtc();
        DateTime? requestedEndUtc = requestedEndDate.ToUtc();

        if (requestedEndUtc is { } explicitEndDate)
        {
            ApplyExplicitEndDate(task, newStatus, explicitEndDate);
        }
        else if (newStatus == ProjectTaskStatus.Completed)
        {
            if (task.EndDate is null)
            {
                task.EndDate = nowUtc < task.StartDate ? task.StartDate : nowUtc;
                task.EndDateWasAuto = true;
            }
            else
            {
                EnsureValidEndDate(task, task.EndDate.Value);
            }
        }
        else if (task.Status == ProjectTaskStatus.Completed && task.EndDateWasAuto)
        {
            task.EndDate = null;
            task.EndDateWasAuto = false;
        }
        else if (task.EndDate is { } preservedEndDate)
        {
            EnsureValidEndDate(task, preservedEndDate);
        }

        task.Status = newStatus;
        task.UpdatedAt = nowUtc;
    }

    private static void EnsureDefinedStatus(ProjectTaskStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported task status.");
        }
    }

    private static void ApplyExplicitEndDate(ProjectTask task, ProjectTaskStatus newStatus, DateTime explicitEndDate)
    {
        bool preserveAutoFlag = task.EndDateWasAuto
            && task.Status == ProjectTaskStatus.Completed
            && newStatus == ProjectTaskStatus.Completed
            && task.EndDate is { } currentEndDate
            && Math.Abs((currentEndDate - explicitEndDate).Ticks) < TimeSpan.TicksPerMillisecond;

        if (preserveAutoFlag)
        {
            EnsureValidEndDate(task, task.EndDate!.Value);
        }
        else
        {
            EnsureValidEndDate(task, explicitEndDate);
            task.EndDate = explicitEndDate;
        }

        task.EndDateWasAuto = preserveAutoFlag;
    }

    private static void EnsureValidEndDate(ProjectTask task, DateTime endDate)
    {
        if (endDate < task.StartDate)
        {
            throw new InvalidDateRangeException();
        }
    }
}
