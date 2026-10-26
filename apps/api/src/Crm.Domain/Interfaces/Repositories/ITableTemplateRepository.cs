using Crm.Domain.Common;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

public interface ITableTemplateRepository : IGenericRepository<TableTemplate>
{
    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of projected table templates.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected elements (e.g., DTO).</typeparam>
    /// <param name="onlySystemPresets">Optional filter to retrieve only system presets (true), user presets (false), or all (null).</param>
    /// <param name="searchTerm">Optional search term to filter templates by Name or Description.</param>
    /// <param name="orderBy">The name of the property to sort by.</param>
    /// <param name="sortOrder">The sort direction (<see cref="SortOrderConstants.Ascending"/> or <see cref="SortOrderConstants.Descending"/>). Defaults to <see cref="SortOrderConstants.Ascending"/>.</param>
    /// <param name="pageNumber">The current page number (1-based index).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated collection of projected template items.</returns>
    Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        bool? onlySystemPresets = null,
        string? searchTerm = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a custom table template by its unique identifier along with its fields for updating, with change tracking enabled.
    /// </summary>
    /// <param name="id">The unique identifier of the table template to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The tracked <see cref="TableTemplate"/> entity including its related <see cref="TableTemplate.Fields"/>.</returns>
    /// <exception cref="NotFoundException">Thrown if the table template with the specified ID is not found.</exception>
    Task<TableTemplate> GetForUpdateWithFieldsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a list of existing system preset names to avoid duplicate seed inserts.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of system preset names.</returns>
    Task<List<string>> GetSystemPresetNamesAsync(CancellationToken cancellationToken = default);
}
