using Crm.Domain.Exceptions;

namespace Crm.Domain.ValueObjects;

/// <summary>
/// Represents an immutable external HTTP or HTTPS link attached to a project task.
/// </summary>
public sealed record TaskExternalLink
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaskExternalLink"/> class with normalized values.
    /// </summary>
    /// <param name="url">The external HTTP or HTTPS URL.</param>
    /// <param name="title">An optional display title for the external link.</param>
    public TaskExternalLink(string url, string? title = null)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new DomainValidationException("External link URL cannot be empty.");
        }

        Url = url.Trim();
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskExternalLink"/> class for EF Core materialization.
    /// </summary>
    private TaskExternalLink()
    {
    }

    /// <summary>
    /// Gets the optional display title for the external link.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the external HTTP or HTTPS URL.
    /// </summary>
    public string Url { get; init; } = string.Empty;
}
