using Crm.Application.Dtos.Users;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;

namespace Crm.Infrastructure.Services.Identity;

/// <summary>
/// Orchestrates opaque action token issuance, AES-256-GCM metadata protection, lookup, paged listing,
/// atomic consumption, and revocation on top of <see cref="IUserActionTokenRepository"/>.
/// </summary>
/// <param name="repository">The persistence repository for hashed action token records.</param>
/// <param name="tokenService">The cryptographic token generator providing opaque tokens and lifetimes.</param>
/// <param name="protector">The symmetric AEAD protector encrypting token metadata.</param>
public sealed class UserActionTokenService(
    IUserActionTokenRepository repository,
    ITokenService tokenService,
    IActionTokenProtector protector) : IUserActionTokenService
{
    /// <inheritdoc />
    public async Task<TokenResultDto> IssueAsync<TMetadata>(
        ActionTokenType type,
        string targetEmail,
        TMetadata metadata,
        Guid? targetUserId = null,
        Guid? createdBy = null,
        CancellationToken cancellationToken = default)
    {
        string trimmedEmail = targetEmail.Trim();
        TokenResultDto tokenResult = tokenService.GenerateActionToken(type);

        var entity = new UserActionToken
        {
            Type = type,
            TargetEmail = trimmedEmail,
            TargetUserId = targetUserId,
            EncryptedMetadata = protector.Protect(metadata, type, trimmedEmail),
            ExpiresAt = tokenResult.ExpiresAt,
            CreatedBy = createdBy is null || createdBy == Guid.Empty ? null : createdBy,
        };

        await repository.CreateAsync(entity, tokenResult.Token, cancellationToken);

        return tokenResult;
    }

    /// <inheritdoc />
    public async Task<ResolvedActionToken<TMetadata>?> ResolveActiveAsync<TMetadata>(
        string rawToken,
        ActionTokenType type,
        CancellationToken cancellationToken = default)
    {
        UserActionTokenRecord? token = type == ActionTokenType.PasswordReset
            ? await repository.GetActiveByRawTokenAsync<UserActionTokenRecord>(rawToken, type, cancellationToken)
            : ToUserActionTokenRecord(await repository.GetActiveByRawTokenAsync<ActionTokenRecord>(rawToken, type, cancellationToken));

        if (token is null
            || string.IsNullOrWhiteSpace(token.TargetEmail)
            || string.IsNullOrWhiteSpace(token.EncryptedMetadata))
        {
            return null;
        }

        TMetadata? metadata = protector.Unprotect<TMetadata>(token.EncryptedMetadata, type, token.TargetEmail);
        return metadata is null ? null : new ResolvedActionToken<TMetadata>(token, metadata);
    }

    /// <inheritdoc />
    public async Task<PagedResult<ResolvedActionToken<TMetadata>>> GetPagedActiveWithMetadataAsync<TMetadata>(
        ActionTokenType type,
        UserActionTokenFilter filter,
        CancellationToken cancellationToken = default)
    {
        PagedResult<ActionTokenRecord> page = await repository.GetPagedActiveAsync<ActionTokenRecord>(
            type,
            filter,
            cancellationToken);

        var resolved = new List<ResolvedActionToken<TMetadata>>(page.Items.Count);
        foreach (ActionTokenRecord record in page.Items)
        {
            if (string.IsNullOrWhiteSpace(record.EncryptedMetadata))
            {
                continue;
            }

            TMetadata? metadata = protector.Unprotect<TMetadata>(record.EncryptedMetadata, type, record.Email);
            if (metadata is not null)
            {
                resolved.Add(new ResolvedActionToken<TMetadata>(ToUserActionTokenRecord(record)!, metadata));
            }
        }

        return new PagedResult<ResolvedActionToken<TMetadata>>(
            resolved,
            page.TotalCount,
            page.PageNumber,
            page.PageSize);
    }

    /// <inheritdoc />
    public Task<PagedResult<TResult>> GetPagedActiveAsync<TResult>(
        ActionTokenType type,
        UserActionTokenFilter filter,
        CancellationToken cancellationToken = default)
        => repository.GetPagedActiveAsync<TResult>(type, filter, cancellationToken);

    /// <inheritdoc />
    public Task<bool> TryConsumeAsync(Guid id, ActionTokenType type, CancellationToken cancellationToken = default)
        => repository.TryConsumeAsync(id, type, cancellationToken);

    /// <inheritdoc />
    public async Task RevokeByIdAsync(Guid id, ActionTokenType type, CancellationToken cancellationToken = default)
    {
        bool deleted = await repository.DeleteByIdAsync(id, type, cancellationToken);
        if (!deleted)
        {
            throw new EntityNotFoundException<UserActionToken>(id);
        }
    }

    /// <inheritdoc />
    public Task RevokeAllByTargetEmailAsync(string email, ActionTokenType type, CancellationToken cancellationToken = default)
        => repository.RevokeAllByTargetEmailAsync(email, type, cancellationToken);

    private static UserActionTokenRecord? ToUserActionTokenRecord(ActionTokenRecord? record)
        => record is null
            ? null
            : new UserActionTokenRecord(
                record.Id,
                record.Type,
                record.Email,
                null,
                null,
                null,
                null,
                record.EncryptedMetadata,
                record.CreatedAt,
                record.ExpiresAt,
                record.CreatedBy);
}
