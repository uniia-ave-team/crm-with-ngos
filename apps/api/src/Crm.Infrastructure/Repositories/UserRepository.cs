using System.Text.Json.Nodes;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Auditing;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides repository implementation for managing <see cref="User"/> entities.
/// </summary>
/// <param name="appDbContext">The application database context.</param>
public sealed class UserRepository(
    ApplicationDbContext appDbContext)
    : GenericRepository<User>(appDbContext),
    IUserRepository
{
    /// <inheritdoc />
    public async Task<string> GetAvatarAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var projection = await DbSet
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new { x.Avatar })
            .FirstOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException<User>(userId);

        return string.IsNullOrWhiteSpace(projection.Avatar)
            ? throw new EntityFieldNotFoundException<User>(nameof(User.Avatar))
            : projection.Avatar;
    }

    /// <inheritdoc />
    public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
        => DbSet.AsNoTracking().AnyAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var isActive = await DbSet
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (bool?)u.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        return isActive ?? throw new EntityNotFoundException<User>(userId);
    }

    /// <inheritdoc />
    public Task<int> AssignUnassignedUsersToNgoAsync(Guid ngoId, CancellationToken cancellationToken = default)
        => DbSet
            .Where(u => u.NgoId == null || u.NgoId == Guid.Empty)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.NgoId, ngoId), cancellationToken);

    /// <inheritdoc />
    public async Task<int> SetUserActivationStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
    {
        int rowsAffected = await DbSet
            .Where(u => u.Id == userId && u.IsActive != isActive)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, isActive), cancellationToken);

        if (rowsAffected > 0)
        {
            Context.TryEnqueueBulkUpdateAudit<User>(
                userId.ToString(),
                new Dictionary<string, AuditPropertyChange>(StringComparer.Ordinal)
                {
                    [nameof(User.IsActive)] = new AuditPropertyChange(
                        JsonValue.Create(!isActive),
                        JsonValue.Create(isActive)),
                });
        }

        return rowsAffected;
    }

    /// <inheritdoc />
    public async Task<Dictionary<Guid, bool>> GetActiveStatusesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        List<Guid> distinctIds = [.. ids.Distinct()];
        return distinctIds.Count == 0
            ? []
            : await DbSet
                .AsNoTracking()
                .Where(u => distinctIds.Contains(u.Id))
                .Select(u => new { u.Id, u.IsActive })
                .ToDictionaryAsync(u => u.Id, u => u.IsActive, cancellationToken);
    }

    /// <inheritdoc />
    public Task UpdateLastLoginAtAsync(Guid userId, DateTime lastLoginAt, CancellationToken cancellationToken = default)
        => DbSet
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, lastLoginAt), cancellationToken);

    /// <inheritdoc />
    public Task<int> SyncCustomFieldsSchemaAsync(SchemaSyncPlan plan, CancellationToken cancellationToken = default)
        => !plan.RequiresJsonbSync
            ? Task.FromResult(0)
            : DbSet.ExecuteUpdateAsync(
                s => s.SetProperty(
                    u => u.CustomFields,
                    u => JsonbFunctionsExtensions.SyncJsonbKeys(u.CustomFields, plan.KeysToDrop, plan.KeysToBackfill)),
                cancellationToken);
}
