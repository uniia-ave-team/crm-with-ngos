namespace Crm.Application.Common.Consts;

/// <summary>
/// Provides shared error message constants for task and task comment operations.
/// </summary>
public static class TaskErrorMessages
{
    /// <summary>
    /// Error message when a user other than the comment author or a project manager attempts to delete a comment.
    /// </summary>
    public const string CommentDeleteForbidden = "Only the comment author or a project manager can delete this comment.";

    /// <summary>
    /// Error message when a user other than the comment author attempts to update a comment.
    /// </summary>
    public const string CommentUpdateForbidden = "Only the comment author can update this comment.";
}
