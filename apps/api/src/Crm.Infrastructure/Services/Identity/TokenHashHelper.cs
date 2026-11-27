using System.Security.Cryptography;
using System.Text;

namespace Crm.Infrastructure.Services.Identity;

/// <summary>
/// Provides shared deterministic SHA-256 hashing for opaque security tokens stored in the database.
/// </summary>
internal static class TokenHashHelper
{
    /// <summary>
    /// Computes the uppercase hexadecimal SHA-256 digest of the specified raw token string.
    /// </summary>
    /// <param name="token">The raw opaque token to hash.</param>
    /// <returns>The 64-character uppercase hex SHA-256 digest.</returns>
    public static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
