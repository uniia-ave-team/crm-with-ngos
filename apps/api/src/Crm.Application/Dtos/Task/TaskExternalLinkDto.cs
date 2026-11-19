namespace Crm.Application.Dtos.Task;

/// <summary>
/// Represents an external HTTP or HTTPS link attached to a project task.
/// </summary>
/// <param name="Title">An optional display title for the external link.</param>
/// <param name="Url">The external HTTP or HTTPS URL.</param>
public record TaskExternalLinkDto(
    string? Title,
    string Url);
