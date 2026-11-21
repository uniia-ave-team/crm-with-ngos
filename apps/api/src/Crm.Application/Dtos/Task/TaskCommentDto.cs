namespace Crm.Application.Dtos.Task;

/// <summary>
/// Represents a comment posted on a task, including author identity and timestamps.
/// </summary>
/// <param name="Id">The unique identifier of the comment.</param>
/// <param name="TaskId">The unique identifier of the task.</param>
/// <param name="UserId">The unique identifier of the comment author, if still available.</param>
/// <param name="AuthorFirstName">The first name of the comment author, if still available.</param>
/// <param name="AuthorLastName">The last name of the comment author, if still available.</param>
/// <param name="AuthorPatronymic">The optional patronymic (middle name) of the comment author, if still available.</param>
/// <param name="AuthorAvatarUrl">The resolved avatar URL of the comment author, if set.</param>
/// <param name="Content">The text content of the comment.</param>
/// <param name="CreatedAt">The UTC timestamp when the comment was created.</param>
/// <param name="UpdatedAt">The UTC timestamp when the comment was last updated.</param>
public record TaskCommentDto(
    Guid Id,
    Guid TaskId,
    Guid? UserId,
    string? AuthorFirstName,
    string? AuthorLastName,
    string? AuthorPatronymic,
    string? AuthorAvatarUrl,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt);
