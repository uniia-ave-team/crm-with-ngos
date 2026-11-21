namespace Crm.Application.Common.Consts;

/// <summary>
/// Provides route template constants and dynamic URL builders for project and task endpoints.
/// </summary>
public static class ApiRouteConstants
{
    /// <summary>
    /// The versioned route prefix for project-scoped task endpoints.
    /// </summary>
    public const string ProjectTasksRoutePrefix = "api/v{version:apiVersion}/projects/{projectId:guid}/tasks";

    /// <summary>
    /// The relative route template for downloading a specific task attachment stream.
    /// </summary>
    public const string TaskAttachmentFileRouteTemplate = "{taskId:guid}/attachments/{attachmentId:guid}";

    /// <summary>
    /// Generates a dynamic route for retrieving a specific task attachment file stream.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="attachmentId">The unique identifier of the attachment.</param>
    /// <returns>The formatted route string for downloading the attachment.</returns>
    public static string TaskAttachmentFile(Guid projectId, Guid taskId, Guid attachmentId)
        => $"api/v1/projects/{projectId}/tasks/{taskId}/attachments/{attachmentId}";
}
