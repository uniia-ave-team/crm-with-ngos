using Crm.Application.Dtos.Tasks;
using Crm.Domain.ValueObjects;

namespace Crm.Application.Features.Tasks.Extensions;

/// <summary>
/// Provides extension methods for normalizing task external link DTOs into domain value objects.
/// </summary>
internal static class TaskExternalLinkExtensions
{
    /// <summary>
    /// Converts and trims an optional collection of <see cref="TaskExternalLinkDto"/> items into domain <see cref="TaskExternalLink"/> objects.
    /// </summary>
    /// <param name="links">The optional external link DTO collection supplied by the client.</param>
    /// <returns>A normalized list of <see cref="TaskExternalLink"/> instances, or an empty list when <paramref name="links"/> is <c>null</c>.</returns>
    public static List<TaskExternalLink> NormalizeExternalLinks(this IReadOnlyList<TaskExternalLinkDto>? links)
        => links is null
            ? []
            : [.. links.Select(l => new TaskExternalLink(l.Url, l.Title))];
}
