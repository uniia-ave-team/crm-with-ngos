using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Features.Projects.Commands;

/// <summary>
/// Represents a command to update a project-scoped tag's title.
/// </summary>
/// <param name="Title">The updated display title of the tag.</param>
public record UpdateTagCommand(
    string Title) : ICommand, ITransactionalCommand, IProjectScoped
{
    /// <summary>
    /// Gets the unique identifier of the parent project, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid ProjectId { get; init; }

    /// <summary>
    /// Gets the unique identifier of the tag to update, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid TagId { get; init; }

    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.Edit;
}
