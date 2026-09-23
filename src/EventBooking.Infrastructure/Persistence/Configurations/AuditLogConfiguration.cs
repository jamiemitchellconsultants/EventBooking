using EventBooking.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_log");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.EntityType).HasColumnName("entity_type").HasMaxLength(50).IsRequired();
        builder.Property(a => a.EntityId).HasColumnName("entity_id");
        builder.Property(a => a.Action).HasColumnName("action").HasConversion<int>();
        builder.Property(a => a.ActorType).HasColumnName("actor_type").HasConversion<int>();
        builder.Property(a => a.ActorId).HasColumnName("actor_id").HasMaxLength(100);
        builder.Property(a => a.Timestamp).HasColumnName("timestamp");
        builder.Property(a => a.Details).HasColumnName("details").HasMaxLength(1000);

        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => new { a.Timestamp, a.Id })
            .HasDatabaseName("ix_audit_log_timestamp")
            .IsDescending(true, true);
    }
}
