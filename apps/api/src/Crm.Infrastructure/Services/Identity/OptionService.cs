using Crm.Application.Dtos.Common;
using Crm.Application.Interfaces;
using Crm.Infrastructure.Options;
using Mapster;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.Services.Identity;

/// <summary>
/// Implements <see cref="IOptionService"/> to expose configured custom identity options
/// and security policies to the application layer.
/// </summary>
/// <param name="identityOptions">The accessor for configured custom identity options containing user and password requirements.</param>
public sealed class OptionService(IOptions<CustomIdentityOptions> identityOptions) : IOptionService
{
    private readonly IdentityOptionsDto _identityOptions = identityOptions.Value.Adapt<IdentityOptionsDto>();

    /// <inheritdoc />
    public IdentityOptionsDto GetIdentityOptions() => _identityOptions;
}
