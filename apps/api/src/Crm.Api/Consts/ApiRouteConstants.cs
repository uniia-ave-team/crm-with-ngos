namespace Crm.Api.Consts;

/// <summary>
/// Provides versioned route template constants for API controllers.
/// </summary>
public static class ApiRouteConstants
{
    /// <summary>
    /// The default versioned route template for API controllers.
    /// </summary>
    public const string DefaultControllerRoute = "api/v{version:apiVersion}/[controller]";

    /// <summary>
    /// The versioned route prefix for project-scoped task endpoints.
    /// </summary>
    public const string ProjectTasksRoutePrefix = "api/v{version:apiVersion}/projects/{projectId:guid}/tasks";
}
