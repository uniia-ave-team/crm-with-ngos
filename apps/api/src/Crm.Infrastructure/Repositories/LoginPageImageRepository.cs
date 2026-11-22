using System.Linq.Expressions;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Expressions;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Implements the repository for managing <see cref="LoginPageImage"/> entities.
/// Inherits standard data access operations from <see cref="GenericRepository{T}"/>.
/// </summary>
/// <param name="appDbContext">The application database context.</param>
public sealed class LoginPageImageRepository(
    ApplicationDbContext appDbContext)
    : GenericRepository<LoginPageImage>(appDbContext),
    ILoginPageImageRepository
{
    /// <inheritdoc />
    public async Task<TResult> GetRandomAsync<TResult>(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .OrderBy(_ => Guid.NewGuid())
            .ProjectToType<TResult>()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException<LoginPageImage>();
    }

    /// <inheritdoc />
    public async Task<string> GetUrlAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await DbSet
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.Url })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException<LoginPageImage>(id);

        return string.IsNullOrWhiteSpace(result.Url)
            ? throw new EntityFieldNotFoundException<LoginPageImage>(nameof(LoginPageImage.Url))
            : result.Url;
    }

    /// <inheritdoc />
    public Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        string? searchTerm = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => GetPagedAsync<TResult>(
            predicate: BuildImageFilter(searchTerm),
            orderBy: orderBy,
            sortOrder: sortOrder,
            pageNumber: pageNumber,
            pageSize: pageSize,
            cancellationToken: cancellationToken);

    private static Expression<Func<LoginPageImage, bool>>? BuildImageFilter(string? searchTerm)
        => string.IsNullOrWhiteSpace(searchTerm)
            ? null
            : QueryFilterExpressions.ContainsIgnoreCase<LoginPageImage>(image => image.Url, searchTerm);
}
