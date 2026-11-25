using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Features.Projects.Commands;

/// <summary>
/// Represents a command to delete a project and all its associated tasks, topics, tags, and member assignments.
/// </summary>
/// <param name="Id">The unique identifier of the project to delete.</param>
public record DeleteProjectCommand(Guid Id) : ICommand, ITransactionalCommand, IProjectScoped, ICurrentUserAware
{
    /// <inheritdoc />
    [JsonIgnore]
    public Guid CurrentUserId { get; set; }

    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => Id;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.Manage;
}
