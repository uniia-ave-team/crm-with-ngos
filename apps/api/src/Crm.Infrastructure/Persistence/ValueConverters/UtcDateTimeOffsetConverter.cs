using Crm.Domain.Extensions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Crm.Infrastructure.Persistence.ValueConverters;

/// <summary>
/// EF Core value converter guaranteeing that every persisted <see cref="DateTimeOffset"/> value
/// is normalized to UTC+0 (<see cref="TimeSpan.Zero"/> offset) on both write and read.
/// </summary>
internal sealed class UtcDateTimeOffsetConverter()
    : ValueConverter<DateTimeOffset, DateTimeOffset>(
        toProvider => toProvider.ToUtc(),
        fromProvider => fromProvider.ToUtc());
