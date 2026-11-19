using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;

namespace Crm.Domain.Extensions;

/// <summary>
/// Provides domain extension methods for <see cref="ProjectTask"/> status transitions and date invariant enforcement.
/// </summary>
public static class ProjectTaskExtensions
{
    /// <summary>
    /// Transitions the task to <paramref name="newStatus"/> while enforcing the <c>EndDate &gt;= StartDate</c> invariant,
    /// auto-populating <see cref="ProjectTask.EndDate"/> only when completing without an end date, and clearing auto-populated end dates on reopen.
    /// </summary>
    /// <param name="task">The task entity to transition.</param>
    /// <param name="newStatus">The target execution status.</param>
    /// <param name="requestedEndDate">An optional explicit UTC end date supplied by the client.</param>
    /// <param name="utcNow">The current UTC timestamp.</param>
    /// <param name="updateEndDateWhenNull">
    /// <c>true</c> for full task creation/replacement (<c>POST</c>/<c>PUT</c>) where a <c>null</c> end date clears an existing value;
    /// <c>false</c> for partial status transitions (<c>PATCH</c>) where a <c>null</c> end date preserves manual/planned dates.
    /// </param>
    /// <exception cref="InvalidTaskDateRangeException">Thrown when the resulting end date is earlier than <see cref="ProjectTask.StartDate"/>.</exception>
    public static void ChangeStatus(
        this ProjectTask task,
        ProjectTaskStatus newStatus,
        DateTime? requestedEndDate,
        DateTime utcNow,
        bool updateEndDateWhenNull = false)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (requestedEndDate is { } explicitEndDate)
        {
            task.EnsureValidEndDate(explicitEndDate);
            task.EndDate = explicitEndDate;
            task.EndDateWasAuto = false;
        }
        else if (updateEndDateWhenNull)
        {
            bool alreadyCompletedWithDate = task.Status == ProjectTaskStatus.Completed && task.EndDate is not null;

            if (newStatus == ProjectTaskStatus.Completed)
            {
                if (!alreadyCompletedWithDate)
                {
                    task.EnsureValidEndDate(utcNow);
                    task.EndDate = utcNow;
                    task.EndDateWasAuto = true;
                }
            }
            else
            {
                task.EndDate = null;
                task.EndDateWasAuto = false;
            }
        }
        else
        {
            if (newStatus == ProjectTaskStatus.Completed)
            {
                if (task.EndDate is null)
                {
                    task.EndDate = utcNow < task.StartDate ? task.StartDate : utcNow;
                    task.EndDateWasAuto = true;
                }
            }
            else if (task.Status == ProjectTaskStatus.Completed && task.EndDateWasAuto)
            {
                task.EndDate = null;
                task.EndDateWasAuto = false;
            }
        }

        task.Status = newStatus;
        task.UpdatedAt = utcNow;
    }

    private static void EnsureValidEndDate(this ProjectTask task, DateTime endDate)
    {
        if (endDate < task.StartDate)
        {
            throw new InvalidTaskDateRangeException();
        }
    }
}
