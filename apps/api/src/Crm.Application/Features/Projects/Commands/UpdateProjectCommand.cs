using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Features.Projects.Commands;

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
    DateTime? EndDate = null) : ICommand, ITransactionalCommand, IProjectScoped, ICurrentUserAware
{
    /// <summary>
    /// Gets the unique identifier of the project to update, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid Id { get; init; }

    /// <summary>
    /// Gets or sets the unique identifier of the currently authenticated user performing the update, populated by the request pipeline.
    /// </summary>
    [JsonIgnore]
    public Guid CurrentUserId { get; set; }

    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => Id;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.Edit;
}
