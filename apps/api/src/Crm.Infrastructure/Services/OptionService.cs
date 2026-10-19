using Crm.Application.Dtos.Common;
using Crm.Application.Interfaces;
using Crm.Infrastructure.Options;
using Mapster;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.Services;

/// <summary>
/// Implements <see cref="IOptionService"/> to expose configured custom identity options
/// and security policies to the application layer.
/// </summary>
/// <param name="identityOptions">The accessor for configured custom identity options containing user and password requirements.</param>
public class OptionService(IOptions<CustomIdentityOptions> identityOptions) : IOptionService
{
    private readonly CustomIdentityOptions _options = identityOptions.Value;

    /// <inheritdoc />
    public IdentityOptionsDto GetIdentityOptions() => _options.Adapt<IdentityOptionsDto>();
}
