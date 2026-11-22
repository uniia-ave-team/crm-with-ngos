using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Defines repository operations for managing <see cref="TaskAttachment"/> entities.
/// </summary>
public interface ITaskAttachmentRepository : IGenericRepository<TaskAttachment>
{
    /// <summary>
    /// Asynchronously retrieves and projects all file attachments for a specific task, ordered by creation date descending.
    /// </summary>
    /// <typeparam name="TResult">The target DTO or projection type.</typeparam>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of projected task attachment records.</returns>
    Task<List<TResult>> GetByTaskIdAsync<TResult>(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a specific task attachment by task ID and attachment ID.
    /// </summary>
    /// <param name="taskId">The unique identifier of the parent task.</param>
    /// <param name="attachmentId">The unique identifier of the attachment.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="TaskAttachment"/> entity.</returns>
    /// <exception cref="EntityNotFoundException{TaskAttachment}">Thrown if the attachment is not found on the task.</exception>
    Task<TaskAttachment> GetByTaskAndIdAsync(Guid taskId, Guid attachmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves the current attachment count and combined file size in bytes for a specific task.
    /// </summary>
    /// <param name="taskId">The unique identifier of the task.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="TaskAttachmentStats"/> record containing the attachment count and total size in bytes.</returns>
    Task<TaskAttachmentStats> GetTaskAttachmentStatsAsync(Guid taskId, CancellationToken cancellationToken = default);
}
