using Crm.Domain.Entities;
using Crm.Domain.Exceptions;

namespace Crm.Domain.Extensions;

/// <summary>
/// Provides domain behavior extension methods for the <see cref="Project"/> entity.
/// </summary>
public static class ProjectExtensions
{
    /// <summary>
    /// Normalizes <see cref="Project.StartDate"/> and <see cref="Project.EndDate"/> to UTC+0
    /// and validates that <see cref="Project.EndDate"/>, when set, is not earlier than <see cref="Project.StartDate"/>.
    /// </summary>
    /// <param name="project">The project entity to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="project"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidDateRangeException">Thrown when <see cref="Project.EndDate"/> precedes <see cref="Project.StartDate"/>.</exception>
    public static void EnsureValidDateRange(this Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        project.StartDate = project.StartDate.ToUtc();
        project.EndDate = project.EndDate.ToUtc();

        if (project.EndDate is { } endDate && endDate < project.StartDate)
        {
            throw new InvalidDateRangeException();
        }
    }
}
