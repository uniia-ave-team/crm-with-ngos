using Crm.Domain.Enums;

namespace Crm.Application.Dtos.Projects;

/// <summary>
/// Represents summary metadata of a project visible to the user.
/// </summary>
/// <param name="Id">The unique identifier of the project.</param>
/// <param name="Title">The display title of the project.</param>
/// <param name="Description">The optional description of the project.</param>
/// <param name="Visibility">The view and edit visibility mode of the project.</param>
/// <param name="StartDate">The UTC start date/time of the project.</param>
/// <param name="EndDate">The optional UTC end date/time of the project.</param>
/// <param name="CreatedBy">The unique identifier of the user who created the project, if still available.</param>
/// <param name="CreatedAt">The UTC timestamp when the project was created.</param>
/// <param name="UpdatedAt">The UTC timestamp when the project was last updated.</param>
public record ProjectSummaryDto(
    Guid Id,
    string Title,
    string? Description,
    ProjectVisibility Visibility,
    DateTime StartDate,
    DateTime? EndDate,
    Guid? CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt);
