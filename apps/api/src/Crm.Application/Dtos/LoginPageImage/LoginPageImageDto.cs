namespace Crm.Application.Dtos.LoginPageImage;

/// <summary>
/// Represents a data transfer object containing login page image details.
/// </summary>
/// <param name="Id">The unique identifier of the login page image.</param>
/// <param name="Url">The URL or storage path pointing to the login page image.</param>
public record LoginPageImageDto(
    Guid Id,
    string Url);
