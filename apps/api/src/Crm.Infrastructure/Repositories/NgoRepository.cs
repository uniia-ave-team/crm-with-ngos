using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Extensions;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Implements the repository for managing <see cref="Ngo"/> entities.
/// Inherits standard data access operations from <see cref="GenericRepository{T}"/>.
/// </summary>
/// <param name="appDbContext">The database context instance.</param>
public sealed class NgoRepository(
    ApplicationDbContext appDbContext)
    : GenericRepository<Ngo>(appDbContext),
    INgoRepository
{
    /// <inheritdoc />
    public async Task EnsureDoesNotExistAsync(CancellationToken cancellationToken = default)
    {
        bool exists = await DbSet
            .AsNoTracking()
            .AnyAsync(cancellationToken);

        if (exists)
        {
            throw new EntityAlreadyExistsException(nameof(Ngo));
        }
    }

    /// <inheritdoc />
    public async Task<TResult> GetAsync<TResult>(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .OrderBy(n => n.Id)
            .ProjectToType<TResult>()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException<Ngo>();
    }

    /// <inheritdoc />
    public async Task<Ngo> GetForUpdateAsync(CancellationToken cancellationToken = default)
    {
        IQueryable<Ngo> query = DbSet;

        if (Context.Database.IsRelational())
        {
            query = query.WithRowLock();
        }

        return await query
            .OrderBy(n => n.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException<Ngo>();
    }

    /// <inheritdoc />
    public async Task<Guid> GetIdAsync(CancellationToken cancellationToken = default)
        => await DbSet
            .AsNoTracking()
            .OrderBy(n => n.Id)
            .Select(n => (Guid?)n.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException<Ngo>();

    /// <inheritdoc />
    public async Task<string> GetLogoAsync(CancellationToken cancellationToken = default)
    {
        var projection = await DbSet
            .AsNoTracking()
            .OrderBy(n => n.Id)
            .Select(x => new { x.Logo })
            .FirstOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException<Ngo>();

        return string.IsNullOrWhiteSpace(projection.Logo)
            ? throw new EntityFieldNotFoundException<Ngo>(nameof(Ngo.Logo))
            : projection.Logo;
    }

    /// <inheritdoc />
    public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
        => DbSet.AsNoTracking().AnyAsync(cancellationToken);
}
