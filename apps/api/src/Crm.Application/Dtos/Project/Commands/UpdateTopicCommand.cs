using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Dtos.Project.Commands;

/// <summary>
/// Represents a command to update a board topic/section's title and display order.
/// </summary>
/// <param name="Title">The updated display title of the topic.</param>
/// <param name="Order">The updated display sort order of the topic column on the board.</param>
public record UpdateTopicCommand(
    string Title,
    byte Order) : IRequest, ITransactionalCommand, IProjectScoped
{
    /// <summary>
    /// Gets the unique identifier of the parent project, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid ProjectId { get; init; }

    /// <summary>
    /// Gets the unique identifier of the topic to update, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid TopicId { get; init; }

    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.Edit;
}
