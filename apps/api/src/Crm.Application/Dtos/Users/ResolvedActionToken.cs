using Crm.Domain.Common;

namespace Crm.Application.Dtos.Users;

/// <summary>
/// Represents an active <see cref="UserActionTokenRecord"/> paired with its authenticated, decrypted metadata payload.
/// </summary>
/// <typeparam name="TMetadata">The typed metadata payload associated with the token.</typeparam>
/// <param name="Token">The lean action token projection.</param>
/// <param name="Metadata">The decrypted metadata payload.</param>
public sealed record ResolvedActionToken<TMetadata>(
    UserActionTokenRecord Token,
    TMetadata Metadata);
