using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;

namespace Crm.Application.Features.Users.Extensions;

/// <summary>
/// Provides extension methods for resolving user-specific media and file URLs.
/// </summary>
public static class UserFileUrlExtensions
{
    /// <summary>
    /// Resolves the absolute API route URL for a user avatar.
    /// </summary>
    /// <param name="fileUrlProvider">The file URL provider instance.</param>
    /// <param name="avatarKey">The file identifier or key stored for the avatar.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="isSelf">Indicates whether the avatar belongs to the currently authenticated user.</param>
    /// <returns>The fully qualified URL to the avatar, or <c>null</c> if no avatar is set.</returns>
    public static string? ResolveUserAvatarUrl(
        this IFileUrlProvider fileUrlProvider,
        string? avatarKey,
        Guid userId,
        bool isSelf = false)
    {
        if (string.IsNullOrWhiteSpace(avatarKey))
        {
            return null;
        }

        string routeTemplate = isSelf
            ? ApiRouteLogoConstants.UserSelfAvatar
            : ApiRouteLogoConstants.UserAvatar(userId);

        return fileUrlProvider.GetFileUrl(avatarKey, routeTemplate);
    }
}
