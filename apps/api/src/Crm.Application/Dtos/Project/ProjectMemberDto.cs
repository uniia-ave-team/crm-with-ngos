using Crm.Domain.Enums;

namespace Crm.Application.Dtos.Project;

/// <summary>
/// Represents a user assigned to a project along with their project-specific functional role title and permission claim.
/// </summary>
/// <param name="ProjectId">The unique identifier of the project.</param>
/// <param name="UserId">The unique identifier of the member user.</param>
/// <param name="FirstName">The first name of the member user.</param>
/// <param name="LastName">The last name of the member user.</param>
/// <param name="Patronymic">The optional patronymic (middle name) of the member user.</param>
/// <param name="AvatarUrl">The resolved avatar URL of the member user, if set.</param>
/// <param name="RoleTitle">The custom functional role title assigned to the member on this project.</param>
/// <param name="Claim">The hierarchical project permission claim assigned to the member.</param>
public record ProjectMemberDto(
    Guid ProjectId,
    Guid UserId,
    string FirstName,
    string LastName,
    string? Patronymic,
    string? AvatarUrl,
    string RoleTitle,
    ProjectClaim Claim);
