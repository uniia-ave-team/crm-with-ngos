using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;

namespace Crm.Domain.Common;

/// <summary>
/// Encapsulates project access metadata for a specific user and evaluates view, edit, and management permissions
/// based strictly on project visibility and explicit <see cref="ProjectMember"/> claims.
/// </summary>
/// <param name="Visibility">The project's visibility mode.</param>
/// <param name="Claim">The project-specific permission claim assigned to the user, or <c>null</c> if not a member.</param>
/// <param name="UserId">The unique identifier of the user being evaluated.</param>
public sealed record ProjectAccess(
    ProjectVisibility Visibility,
    ProjectClaim? Claim,
    Guid UserId)
{
    /// <summary>
    /// Gets a value indicating whether the user can view the project and its contents.
    /// </summary>
    public bool CanView => Visibility != ProjectVisibility.Private || Claim is not null;

    /// <summary>
    /// Gets a value indicating whether the user can edit tasks, topics, tags, and general project details.
    /// </summary>
    public bool CanEdit => Visibility == ProjectVisibility.Public || Claim >= ProjectClaim.Edit;

    /// <summary>
    /// Gets a value indicating whether the user can manage project settings, visibility, members, and deletion.
    /// Public visibility does not grant management permissions.
    /// </summary>
    public bool CanManage => Claim >= ProjectClaim.Manage;

    /// <summary>
    /// Ensures the user satisfies the required <see cref="ProjectClaim"/> level for the project.
    /// Throws <see cref="EntityNotFoundException{Project}"/> if the user cannot view a private project
    /// (to avoid disclosing its existence), or <see cref="UnauthorizedAccessException"/> if view access exists
    /// but the required permission level is not met.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="requiredClaim">The required permission level.</param>
    /// <exception cref="EntityNotFoundException{Project}">Thrown when the user cannot view the project.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the user lacks the required permission level.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="requiredClaim"/> is not a supported <see cref="ProjectClaim"/>.</exception>
    public void EnsureHasClaim(Guid projectId, ProjectClaim requiredClaim)
    {
        if (!CanView)
        {
            throw new EntityNotFoundException<Project>(projectId);
        }

        bool allowed = requiredClaim switch
        {
            ProjectClaim.View => true,
            ProjectClaim.Edit => CanEdit,
            ProjectClaim.Manage => CanManage,
            _ => throw new ArgumentOutOfRangeException(nameof(requiredClaim), requiredClaim, "Unsupported project claim."),
        };

        if (!allowed)
        {
            throw new UnauthorizedAccessException($"User '{UserId}' does not have '{requiredClaim}' permission on project '{projectId}'.");
        }
    }
}
