using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Dtos.Project.Commands;

/// <summary>
/// Represents a command to assign a user to a project with a custom functional role title and hierarchical permission claim.
/// </summary>
/// <param name="UserId">The unique identifier of the user to assign to the project.</param>
/// <param name="RoleTitle">An optional free-text functional role title on the project.</param>
/// <param name="Claim">The hierarchical project permission claim assigned to the member.</param>
public record AssignProjectMemberCommand(
    Guid UserId,
    string? RoleTitle = null,
    ProjectClaim Claim = ProjectClaim.View) : IRequest, ITransactionalCommand, IProjectScoped
{
    /// <summary>
    /// Gets the unique identifier of the project, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid ProjectId { get; init; }

    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.Manage;
}
