namespace Crm.Domain.Common;

/// <summary>
/// Represents an immutable instruction for renaming a record field key safely.
/// </summary>
/// <param name="FieldId">The unique identifier of the field definition being renamed.</param>
/// <param name="OldKey">The original JSON key name.</param>
/// <param name="NewKey">The target JSON key name.</param>
public sealed record KeyRename(Guid FieldId, string OldKey, string NewKey)
{
    /// <summary>
    /// Gets a deterministic temporary key name used during two-phase rename operations to prevent key collisions.
    /// </summary>
    public string TempKey { get; } = $"__rename_{FieldId:N}";
}
