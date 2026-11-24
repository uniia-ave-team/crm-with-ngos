namespace Crm.Application.Dtos.Projects;

/// <summary>
/// Represents a tag scoped to a specific project.
/// </summary>
/// <param name="Id">The unique identifier of the tag.</param>
/// <param name="ProjectId">The unique identifier of the parent project.</param>
/// <param name="Title">The display title of the tag.</param>
public record TagDto(
    Guid Id,
    Guid ProjectId,
    string Title);
