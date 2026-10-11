namespace Crm.Application.Common.Consts;

/// <summary>
/// Provides constant values and route generator methods for file and media endpoints across the application.
/// </summary>
public static class ApiRouteLogoConstants
{
    /// <summary>
    /// The route template for retrieving the single NGO logo.
    /// </summary>
    public const string NgoLogo = "api/v1/ngo/logo";

    /// <summary>
    /// The route template for retrieving the current authenticated user's avatar.
    /// </summary>
    public const string UserSelfAvatar = "api/v1/users/me/avatar";

    /// <summary>
    /// Generates a dynamic route for retrieving a specific user's avatar by their unique identifier.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>The formatted route string containing the user ID.</returns>
    public static string UserAvatar(Guid userId) => $"api/v1/users/{userId}/avatar";

    /// <summary>
    /// Generates a dynamic route for retrieving a specific login page image file by its unique identifier.
    /// </summary>
    /// <param name="imageId">The unique identifier of the login page image.</param>
    /// <returns>The formatted route string containing the image ID.</returns>
    public static string LoginPageImageFile(Guid imageId) => $"api/v1/loginpageimages/{imageId}/image";
}
