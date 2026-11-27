namespace Crm.Domain.Common;

/// <summary>
/// Lightweight projection carrying a <see cref="Entities.CustomRecord"/>'s table ownership and fractional position rank.
/// </summary>
/// <param name="Id">The unique identifier of the custom record.</param>
/// <param name="CustomTableId">The unique identifier of the owning custom table.</param>
/// <param name="Position">The lexicographical fractional-indexing rank of the record.</param>
public sealed record CustomRecordPositionInfo(
    Guid Id,
    Guid CustomTableId,
    string Position);
