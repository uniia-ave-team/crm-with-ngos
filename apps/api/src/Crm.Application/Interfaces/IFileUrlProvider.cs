namespace Crm.Application.Interfaces;

/// <summary>
/// Defines a provider responsible exclusively for resolving API routing URLs for stored files.
/// Extracted into a separate interface to comply with the Interface Segregation Principle (ISP).
/// </summary>
public interface IFileUrlProvider
{
    /// <summary>
    /// Constructs the full API endpoint URL or returns the external URL as-is.
    /// </summary>
    /// <param name="fileIdentifier">The stored file name (guid) or an absolute external URL (e.g., Google avatar).</param>
    /// <param name="routeTemplate">The specific API route template for this entity (e.g., "v1/ngo/logo").</param>
    /// <returns>
    /// The fully qualified URL, or <c>null</c> if the input is null or empty.
    /// </returns>
    string? GetFileUrl(string? fileIdentifier, string routeTemplate);
}
