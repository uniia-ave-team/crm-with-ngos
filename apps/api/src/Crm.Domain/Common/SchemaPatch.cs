namespace Crm.Domain.Common;

/// <summary>
/// Represents a flat schema patch payload for atomic PostgreSQL JSONB updates in a single SQL pass.
/// </summary>
/// <param name="DropKeys">
/// Array of keys to be removed from the JSONB document (deleted fields and old keys after renaming).
/// </param>
/// <param name="TargetKeys">
/// Array of target keys to be written or updated in the JSONB document.
/// </param>
/// <param name="SourceKeys">
/// Array of source keys in the original JSONB document corresponding to <paramref name="TargetKeys"/>.
/// An empty string <c>""</c> indicates no value should be read from an old key.
/// </param>
/// <param name="FallbackJson">
/// Array of raw JSON literals (e.g., <c>""</c>, <c>0</c>, <c>false</c>) used as default backfill values
/// when the source key is missing or resolves to <c>null</c>. An empty string <c>""</c> indicates no fallback is needed.
/// </param>
public sealed record SchemaPatch(
    string[] DropKeys,
    string[] TargetKeys,
    string[] SourceKeys,
    string[] FallbackJson);
