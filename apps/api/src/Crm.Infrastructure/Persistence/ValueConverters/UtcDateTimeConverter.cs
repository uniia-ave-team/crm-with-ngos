using Crm.Domain.Extensions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Crm.Infrastructure.Persistence.ValueConverters;

/// <summary>
/// EF Core value converter guaranteeing that every persisted <see cref="DateTime"/> value
/// is written to PostgreSQL in UTC+0 and materialized back with <see cref="DateTimeKind.Utc"/>.
/// </summary>
internal sealed class UtcDateTimeConverter()
    : ValueConverter<DateTime, DateTime>(
        toProvider => toProvider.ToUtc(),
        fromProvider => DateTime.SpecifyKind(fromProvider, DateTimeKind.Utc));
