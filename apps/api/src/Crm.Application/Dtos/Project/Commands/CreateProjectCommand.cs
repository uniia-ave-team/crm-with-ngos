using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Dtos.Project.Commands;

/// <summary>
/// Represents a command to create a new project.
/// </summary>
/// <param name="Title">The display title of the project.</param>
/// <param name="Description">An optional description of the project.</param>
/// <param name="Visibility">The view and edit visibility mode of the project.</param>
/// <param name="StartDate">The UTC start date/time of the project.</param>
/// <param name="EndDate">An optional UTC target end date/time of the project.</param>
public record CreateProjectCommand(
    string Title,
    string? Description,
    ProjectVisibility Visibility,
    DateTime StartDate,
    DateTime? EndDate = null) : IRequest<Guid>, ITransactionalCommand;
