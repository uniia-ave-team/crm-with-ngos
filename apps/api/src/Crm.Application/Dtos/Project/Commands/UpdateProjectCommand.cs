using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Dtos.Project.Commands;

/// <summary>
/// Represents a command to update an existing project's metadata and visibility settings.
/// </summary>
/// <param name="Title">The updated display title of the project.</param>
/// <param name="Description">The updated optional description of the project.</param>
/// <param name="Visibility">The updated view and edit visibility mode of the project.</param>
/// <param name="StartDate">The updated UTC start date/time of the project.</param>
/// <param name="EndDate">The updated optional UTC end date/time of the project.</param>
public record UpdateProjectCommand(
    string Title,
    string? Description,
    ProjectVisibility Visibility,
    DateTime StartDate,
    DateTime? EndDate = null) : IRequest, ITransactionalCommand, IProjectScoped
{
    /// <summary>
    /// Gets the unique identifier of the project to update, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid Id { get; init; }

    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => Id;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.Edit;
}
