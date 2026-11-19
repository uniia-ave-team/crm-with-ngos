using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;

namespace Crm.Domain.Common;

/// <summary>
/// Encapsulates project access metadata for a specific user and evaluates view, edit, and management permissions.
/// </summary>
/// <param name="Visibility">The project's visibility mode.</param>
/// <param name="CreatedBy">The unique identifier of the project creator, if still available.</param>
/// <param name="Claim">The project-specific permission claim assigned to the user, or <c>null</c> if not a member.</param>
/// <param name="UserId">The unique identifier of the user being evaluated.</param>
public sealed record ProjectAccess(
    ProjectVisibility Visibility,
    Guid? CreatedBy,
    ProjectClaim? Claim,
    Guid UserId)
{
    /// <summary>
    /// Gets a value indicating whether the evaluated user is the project creator/owner.
    /// </summary>
    public bool IsOwner => CreatedBy == UserId;

    /// <summary>
    /// Gets a value indicating whether the user can view the project and its contents.
    /// </summary>
    public bool CanView => Visibility != ProjectVisibility.Private || IsOwner || Claim is not null;

    /// <summary>
    /// Gets a value indicating whether the user can edit tasks, topics, tags, and general project details.
    /// </summary>
    public bool CanEdit => IsOwner || Visibility == ProjectVisibility.Public || Claim >= ProjectClaim.Edit;

    /// <summary>
    /// Gets a value indicating whether the user can manage project settings, visibility, members, and deletion.
    /// Public visibility does not grant management permissions.
    /// </summary>
    public bool CanManage => IsOwner || Claim >= ProjectClaim.Manage;

    /// <summary>
    /// Ensures the user has view access to the project, throwing <see cref="EntityNotFoundException{Project}"/>
    /// when a private project is not visible to avoid disclosing its existence.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <exception cref="EntityNotFoundException{Project}">Thrown when the user cannot view the project.</exception>
    public void EnsureCanView(Guid projectId)
    {
        if (!CanView)
        {
            throw new EntityNotFoundException<Project>(projectId);
        }
    }

    /// <summary>
    /// Ensures the user has edit access to the project.
    /// Throws <see cref="EntityNotFoundException{Project}"/> if the user cannot even view a private project,
    /// or <see cref="UnauthorizedAccessException"/> if the user can view the project but lacks edit permission.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <exception cref="EntityNotFoundException{Project}">Thrown when the user cannot view the project.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the user lacks edit permission.</exception>
    public void EnsureCanEdit(Guid projectId)
    {
        EnsureCanView(projectId);

        if (!CanEdit)
        {
            throw new UnauthorizedAccessException($"User '{UserId}' does not have permission to edit project '{projectId}'.");
        }
    }

    /// <summary>
    /// Ensures the user has management access to the project.
    /// Throws <see cref="EntityNotFoundException{Project}"/> if the user cannot even view a private project,
    /// or <see cref="UnauthorizedAccessException"/> if the user can view the project but lacks manage permission.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <exception cref="EntityNotFoundException{Project}">Thrown when the user cannot view the project.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the user lacks management permission.</exception>
    public void EnsureCanManage(Guid projectId)
    {
        EnsureCanView(projectId);

        if (!CanManage)
        {
            throw new UnauthorizedAccessException($"User '{UserId}' does not have permission to manage project '{projectId}'.");
        }
    }

    /// <summary>
    /// Ensures the user satisfies the required <see cref="ProjectClaim"/> level for the project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="requiredClaim">The required permission level.</param>
    public void EnsureHasClaim(Guid projectId, ProjectClaim requiredClaim)
    {
        EnsureCanView(projectId);

        bool allowed = requiredClaim switch
        {
            ProjectClaim.View => true,
            ProjectClaim.Edit => CanEdit,
            ProjectClaim.Manage => CanManage,
            _ => throw new ArgumentOutOfRangeException(nameof(requiredClaim), requiredClaim, DomainErrorMessages.UnsupportedProjectClaim),
        };

        if (!allowed)
        {
            throw new UnauthorizedAccessException($"User '{UserId}' does not have '{requiredClaim}' permission on project '{projectId}'.");
        }
    }
}
