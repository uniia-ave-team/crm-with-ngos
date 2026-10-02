namespace Crm.Application.Dtos.Ngo;

/// <summary>
/// Represents the detailed data of an NGO transferred to the presentation layer.
/// </summary>
/// <param name="Id">The unique identifier of the NGO.</param>
/// <param name="Name">The name of the NGO.</param>
/// <param name="LogoUrl">The URL or path to the NGO's logo.</param>
/// <param name="CreatedAt">The UTC timestamp when the NGO was created.</param>
public record NgoDto(
    Guid Id,
    string Name,
    string? LogoUrl,
    DateTime CreatedAt);
