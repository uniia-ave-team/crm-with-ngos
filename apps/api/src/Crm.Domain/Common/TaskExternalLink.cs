namespace Crm.Domain.Common;

/// <summary>
/// Represents an external HTTP or HTTPS link attached to a project task.
/// </summary>
public sealed class TaskExternalLink
{
    /// <summary>
    /// Gets or sets the optional display title for the external link.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the external HTTP or HTTPS URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;
}
