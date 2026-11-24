using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Features.Projects.Commands;

/// <summary>
/// Represents a command to update a project member's functional role title and hierarchical permission claim.
/// </summary>
/// <param name="RoleTitle">The updated optional free-text functional role title on the project.</param>
/// <param name="Claim">The updated hierarchical project permission claim assigned to the member.</param>
public record UpdateProjectMemberCommand(
    string? RoleTitle = null,
    ProjectClaim Claim = ProjectClaim.View) : ICommand, ITransactionalCommand, IProjectScoped
{
    /// <summary>
    /// Gets the unique identifier of the project, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid ProjectId { get; init; }

    /// <summary>
    /// Gets the unique identifier of the project member user, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid UserId { get; init; }

    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.Manage;
}
