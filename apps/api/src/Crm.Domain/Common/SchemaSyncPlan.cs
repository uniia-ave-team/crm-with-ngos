namespace Crm.Domain.Common;

/// <summary>
/// Captures the JSONB payload synchronization keys (keys to drop and newly required keys to backfill)
/// produced when reconciling a dynamic field schema.
/// </summary>
/// <param name="KeysToDrop">JSONB keys corresponding to removed fields that should be stripped from stored records.</param>
/// <param name="KeysToBackfill">JSONB keys for newly required fields that should be backfilled with empty strings when missing or null.</param>
public record SchemaSyncPlan(
    string[] KeysToDrop,
    string[] KeysToBackfill)
{
    /// <summary>
    /// Gets a value indicating whether any stored JSONB payloads need to be patched.
    /// </summary>
    public bool RequiresJsonbSync => KeysToDrop.Length > 0 || KeysToBackfill.Length > 0;
}
