namespace Crm.Application.Interfaces;

/// <summary>
/// Provides the build version string of the running application.
/// </summary>
public interface IAppVersionProvider
{
    /// <summary>
    /// Gets the application version (for example, <c>1.4.0</c>, <c>main-3f2c1ab</c>, or <c>dev</c>).
    /// </summary>
    string Version { get; }
}
