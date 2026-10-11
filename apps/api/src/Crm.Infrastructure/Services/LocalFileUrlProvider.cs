using Crm.Application.Interfaces;
using Crm.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.Services;

/// <summary>
/// Implements the <see cref="IFileUrlProvider"/> to construct fully qualified HTTP URLs for locally stored files.
/// Designed to be registered as a Singleton, as it relies purely on configuration state without maintaining any internal state.
/// </summary>
/// <param name="options">The configuration options containing the BaseUrl injected via the options pattern.</param>
public class LocalFileUrlProvider(IOptions<FileStorageOptions> options) : IFileUrlProvider
{
    private readonly FileStorageOptions _options = options.Value;

    /// <inheritdoc />
    public string? GetFileUrl(string? fileIdentifier, string routeTemplate)
    {
        if (string.IsNullOrWhiteSpace(fileIdentifier))
        {
            return null;
        }

        if (IsExternalUrl(fileIdentifier))
        {
            return fileIdentifier;
        }

        string baseUrl = _options.BaseUrl.TrimEnd('/');
        string cleanRoute = routeTemplate.TrimStart('/');
        string cacheBusterVersion = Path.GetFileNameWithoutExtension(fileIdentifier);

        return $"{baseUrl}/{cleanRoute}?v={cacheBusterVersion}";
    }

    /// <summary>
    /// Determines whether the provided string is an absolute external URL rather than a local file name.
    /// </summary>
    /// <param name="url">The file name or URL to evaluate.</param>
    /// <returns><c>true</c> if the string starts with 'http://' or 'https://'; otherwise, <c>false</c>.</returns>
    private static bool IsExternalUrl(string url)
    {
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
