using Crm.Application.Dtos.Project;
using Crm.Application.Features.Users.Extensions;
using Crm.Application.Interfaces;

namespace Crm.Application.Features.Projects.Extensions;

/// <summary>
/// Provides URL enrichment helpers for projected project DTOs.
/// </summary>
public static class ProjectDtoExtensions
{
    /// <summary>
    /// Resolves avatar URLs for all members in a <see cref="ProjectDto"/>.
    /// </summary>
    /// <param name="project">The projected project DTO to enrich.</param>
    /// <param name="fileUrlProvider">The file URL provider used to resolve avatar URLs.</param>
    /// <param name="currentUserId">The unique identifier of the current user.</param>
    /// <returns>A copy of <paramref name="project"/> with resolved member avatar URLs.</returns>
    public static ProjectDto EnrichUrls(
        this ProjectDto project,
        IFileUrlProvider fileUrlProvider,
        Guid currentUserId)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(fileUrlProvider);

        return project with
        {
            Members = project.Members.EnrichUrls(fileUrlProvider, currentUserId),
        };
    }

    /// <summary>
    /// Resolves avatar URLs for a list of <see cref="ProjectMemberDto"/> items.
    /// </summary>
    /// <param name="members">The sequence of project member DTOs to enrich.</param>
    /// <param name="fileUrlProvider">The file URL provider used to resolve avatar URLs.</param>
    /// <param name="currentUserId">The unique identifier of the current user.</param>
    /// <returns>A list of <see cref="ProjectMemberDto"/> items with resolved avatar URLs.</returns>
    public static List<ProjectMemberDto> EnrichUrls(
        this IEnumerable<ProjectMemberDto> members,
        IFileUrlProvider fileUrlProvider,
        Guid currentUserId)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(fileUrlProvider);

        return
        [
            .. members.Select(member => member with
            {
                AvatarUrl = fileUrlProvider.ResolveUserAvatarUrl(
                    member.AvatarUrl,
                    member.UserId,
                    member.UserId == currentUserId),
            }),
        ];
    }
}
