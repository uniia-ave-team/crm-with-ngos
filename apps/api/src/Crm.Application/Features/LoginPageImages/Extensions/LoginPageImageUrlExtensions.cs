using Crm.Application.Common.Consts;
using Crm.Application.Dtos.LoginPageImages;
using Crm.Application.Interfaces;

namespace Crm.Application.Features.LoginPageImages.Extensions;

/// <summary>
/// Provides extension methods for resolving public file URLs on <see cref="LoginPageImageDto"/> instances.
/// </summary>
public static class LoginPageImageUrlExtensions
{
    /// <summary>
    /// Returns a copy of the <see cref="LoginPageImageDto"/> with its <see cref="LoginPageImageDto.Url"/>
    /// resolved to a full public route URL using the specified <see cref="IFileUrlProvider"/>.
    /// </summary>
    /// <param name="dto">The login page image DTO whose URL should be resolved.</param>
    /// <param name="fileUrlProvider">The provider used to construct the full web URL.</param>
    /// <returns>A <see cref="LoginPageImageDto"/> with the resolved URL.</returns>
    public static LoginPageImageDto WithResolvedUrl(this LoginPageImageDto dto, IFileUrlProvider fileUrlProvider)
        => dto with
        {
            Url = fileUrlProvider.GetFileUrl(dto.Url, FileRouteConstants.LoginPageImageFile(dto.Id)) ?? dto.Url,
        };
}
