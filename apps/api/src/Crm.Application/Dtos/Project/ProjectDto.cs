using Crm.Domain.Enums;

namespace Crm.Application.Dtos.Project;

/// <summary>
/// Represents detailed information about a project, including its members, board topics, and project-scoped tags.
/// </summary>
/// <param name="Id">The unique identifier of the project.</param>
/// <param name="Title">The display title of the project.</param>
/// <param name="Description">The optional description of the project.</param>
/// <param name="Visibility">The view and edit visibility mode of the project.</param>
/// <param name="StartDate">The UTC start date/time of the project.</param>
/// <param name="EndDate">The optional UTC end date/time of the project.</param>
/// <param name="CreatedBy">The unique identifier of the user who created the project, if still available.</param>
/// <param name="Members">The list of members assigned to the project.</param>
/// <param name="Topics">The list of board topics/sections belonging to the project.</param>
/// <param name="Tags">The list of project-scoped tags defined in the project.</param>
public record ProjectDto(
    Guid Id,
    string Title,
    string? Description,
    ProjectVisibility Visibility,
    DateTime StartDate,
    DateTime? EndDate,
    Guid? CreatedBy,
    IReadOnlyList<ProjectMemberDto> Members,
    IReadOnlyList<TopicDto> Topics,
    IReadOnlyList<TagDto> Tags);
