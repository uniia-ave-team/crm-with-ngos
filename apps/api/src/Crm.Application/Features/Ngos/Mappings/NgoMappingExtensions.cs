using Crm.Application.Dtos.Ngo;
using Crm.Domain.Entities;

namespace Crm.Application.Features.Ngos.Mappings;

/// <summary>
/// Provides extension methods for mapping NGO domain entities to data transfer objects.
/// </summary>
public static class NgoMappingExtensions
{
    /// <summary>
    /// Maps an NGO domain entity instance to an <see cref="NgoDto"/> in-memory.
    /// </summary>
    /// <param name="ngo">The NGO domain entity to map.</param>
    /// <returns>A mapped <see cref="NgoDto"/> containing the NGO's details, description, logo URL, and creation timestamp.</returns>
    public static NgoDto ToDto(this Ngo ngo) => new(
        ngo.Id,
        ngo.Name,
        ngo.Description,
        ngo.LogoUrl,
        ngo.CreatedAt);
}
