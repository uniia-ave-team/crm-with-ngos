namespace Crm.Application.Dtos.Projects;

/// <summary>
/// Represents a board column or section within a project.
/// </summary>
/// <param name="Id">The unique identifier of the topic.</param>
/// <param name="ProjectId">The unique identifier of the parent project.</param>
/// <param name="Title">The display title of the topic.</param>
/// <param name="Order">The display sort order of the topic column on the board.</param>
public record TopicDto(
    Guid Id,
    Guid ProjectId,
    string Title,
    byte Order);
