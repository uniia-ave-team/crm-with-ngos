using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence.Consts;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="AuditLogEntry"/> table (<c>AuditLogs</c>).
/// </summary>
public sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    private const int MaxEntityTypeLength = 100;
    private const int MaxEntityKeyLength = 200;
    private const int MaxTraceIdLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable(TableNames.AuditLogs);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.OccurredAtUtc)
            .IsRequired();

        builder.Property(e => e.TraceId)
            .HasMaxLength(MaxTraceIdLength);

        builder.Property(e => e.EntityType)
            .IsRequired()
            .HasMaxLength(MaxEntityTypeLength);

        builder.Property(e => e.EntityKey)
            .IsRequired()
            .HasMaxLength(MaxEntityKeyLength);

        builder.Property(e => e.Action)
            .IsRequired();

        builder.Property(e => e.Changes)
            .ConfigureJsonbColumn();

        builder.HasIndex(e => new { e.EntityType, e.EntityKey, e.OccurredAtUtc });
        builder.HasIndex(e => e.ActorUserId);
        builder.HasIndex(e => e.OccurredAtUtc);
    }
}
