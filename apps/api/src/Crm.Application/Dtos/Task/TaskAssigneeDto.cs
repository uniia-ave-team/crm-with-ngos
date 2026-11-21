namespace Crm.Application.Dtos.Task;

/// <summary>
/// Represents a user assigned to a task, including their name components and resolved avatar URL.
/// </summary>
/// <param name="Id">The unique identifier of the assigned user.</param>
/// <param name="FirstName">The first name of the assigned user.</param>
/// <param name="LastName">The last name of the assigned user.</param>
/// <param name="Patronymic">The optional patronymic (middle name) of the assigned user.</param>
/// <param name="AvatarUrl">The resolved avatar URL of the assigned user, if set.</param>
public record TaskAssigneeDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? Patronymic,
    string? AvatarUrl);
