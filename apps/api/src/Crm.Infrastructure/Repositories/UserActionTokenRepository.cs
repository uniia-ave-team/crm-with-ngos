using System.Linq.Expressions;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Expressions;
using Crm.Infrastructure.Persistence.Sorting;
using Crm.Infrastructure.Services.Identity;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides repository implementation for managing <see cref="UserActionToken"/> entities
/// using <see cref="GenericRepository{T}"/> pagination and Mapster SQL projections.
/// </summary>
/// <param name="context">The application database context.</param>
/// <param name="timeProvider">The time provider supplying UTC timestamps for token expiration checks.</param>
public sealed class UserActionTokenRepository(
    ApplicationDbContext context,
    TimeProvider timeProvider)
    : GenericRepository<UserActionToken>(context),
    IUserActionTokenRepository
{
    /// <inheritdoc />
    public async Task CreateAsync(UserActionToken token, string rawToken, CancellationToken cancellationToken = default)
    {
        token.TokenHash = TokenHashHelper.Hash(rawToken);
        token.NormalizedTargetEmail = token.TargetEmail.Trim().ToUpperInvariant();

        if (token.CreatedAt == default)
        {
            token.CreatedAt = timeProvider.GetUtcNow().UtcDateTime;
        }

        await DbSet.AddAsync(token, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResult?> GetActiveByRawTokenAsync<TResult>(
        string rawToken,
        ActionTokenType type,
        CancellationToken cancellationToken = default)
    {
        string tokenHash = TokenHashHelper.Hash(rawToken);
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;

        return DbSet
            .AsNoTracking()
            .Where(t => t.TokenHash == tokenHash && t.Type == type && t.ExpiresAt > utcNow)
            .ProjectToType<TResult>()
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<PagedResult<TResult>> GetPagedActiveAsync<TResult>(
        ActionTokenType type,
        UserActionTokenFilter filter,
        CancellationToken cancellationToken = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;

        IQueryable<UserActionToken> query = DbSet
            .AsNoTracking()
            .Where(t => t.Type == type && t.ExpiresAt > utcNow);

        if (filter.TargetUserId.HasValue)
        {
            Guid userId = filter.TargetUserId.Value;
            query = query.Where(t => t.TargetUserId == userId);
        }

        if (filter.CreatedBy.HasValue)
        {
            Guid createdBy = filter.CreatedBy.Value;
            query = query.Where(t => t.CreatedBy == createdBy);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            query = query.Where(BuildSearchPredicate(type, filter.SearchTerm));
        }

        query = string.IsNullOrWhiteSpace(filter.OrderBy)
            ? query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            : ApplyOrdering<TResult>(query, filter.OrderBy, filter.SortOrder);

        return ToPagedResultAsync<TResult>(query, filter.PageNumber, filter.PageSize, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> TryConsumeAsync(Guid id, ActionTokenType type, CancellationToken cancellationToken = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;

        int rowsAffected = await DbSet
            .Where(t => t.Id == id && t.Type == type && t.ExpiresAt > utcNow)
            .ExecuteDeleteAsync(cancellationToken);

        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteByIdAsync(Guid id, ActionTokenType type, CancellationToken cancellationToken = default)
    {
        int rowsAffected = await DbSet
            .Where(t => t.Id == id && t.Type == type)
            .ExecuteDeleteAsync(cancellationToken);

        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public Task RevokeAllByTargetEmailAsync(string email, ActionTokenType type, CancellationToken cancellationToken = default)
    {
        string normalizedEmail = email.Trim().ToUpperInvariant();

        return DbSet
            .Where(t => t.Type == type && t.NormalizedTargetEmail == normalizedEmail)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task RevokeAllByTargetUserIdAsync(Guid userId, ActionTokenType type, CancellationToken cancellationToken = default)
        => DbSet
            .Where(t => t.Type == type && t.TargetUserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

    /// <inheritdoc />
    public Task<int> RemoveExpiredTokensAsync(CancellationToken cancellationToken = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;

        return DbSet
            .Where(t => t.ExpiresAt <= utcNow)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override IQueryable<UserActionToken> ApplyOrdering<TResult>(
        IQueryable<UserActionToken> query,
        string orderBy,
        string? sortOrder)
    {
        bool isDescending = sortOrder.IsDescending();
        string normalized = orderBy.Trim();

        return normalized switch
        {
            _ when normalized.EqualsAnyIgnoreCase(UserActionTokenExtensions.EmailOrderBy, nameof(UserActionToken.TargetEmail)) => isDescending
                ? query.OrderByDescending(t => t.NormalizedTargetEmail).ThenByDescending(t => t.Id)
                : query.OrderBy(t => t.NormalizedTargetEmail).ThenBy(t => t.Id),

            _ when normalized.EqualsAnyIgnoreCase(UserConstants.FullName, nameof(User.LastName)) => isDescending
                ? query.OrderByDescending(t => t.TargetUser != null ? t.TargetUser.LastName : null)
                    .ThenByDescending(t => t.TargetUser != null ? t.TargetUser.FirstName : null)
                    .ThenByDescending(t => t.Id)
                : query.OrderBy(t => t.TargetUser != null ? t.TargetUser.LastName : null)
                    .ThenBy(t => t.TargetUser != null ? t.TargetUser.FirstName : null)
                    .ThenBy(t => t.Id),

            _ => base.ApplyOrdering<TResult>(query, normalized, sortOrder),
        };
    }

    private static Expression<Func<UserActionToken, bool>> BuildSearchPredicate(ActionTokenType type, string searchTerm)
        => type == ActionTokenType.PasswordReset
            ? QueryFilterExpressions.ContainsIgnoreCase<UserActionToken>(
                searchTerm,
                t => t.TargetEmail,
                t => t.TargetUser != null ? t.TargetUser.FirstName : null,
                t => t.TargetUser != null ? t.TargetUser.LastName : null)
            : QueryFilterExpressions.ContainsIgnoreCase<UserActionToken>(
                searchTerm,
                t => t.TargetEmail);
}
