using Crm.Application.Dtos.Common;

namespace Crm.Application.Interfaces;

/// <summary>
/// Defines a service for accessing application configuration settings and policy options.
/// </summary>
public interface IOptionService
{
    /// <summary>
    /// Retrieves the current identity settings and password complexity requirements.
    /// </summary>
    /// <returns>An <see cref="IdentityOptionsDto"/> containing identity validation rules.</returns>
    IdentityOptionsDto GetIdentityOptions();
}
