using EventBooking.Domain.SelfRegistrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps one anonymous self-registration request.</summary>
public sealed class PendingRegistrationConfiguration : IEntityTypeConfiguration<PendingRegistration>
{
    /// <summary>Configures the request table, idempotency index and sweep index.</summary>
    public void Configure(EntityTypeBuilder<PendingRegistration> builder)
    {
        builder.ToTable("pending_registration");
        builder.HasKey(x => x.RequestId);

        builder.Property(x => x.RequestId).HasColumnName("request_id");
        builder.Property(x => x.EventGroupId).HasColumnName("event_group_id");
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.AttendeeGroupId).HasColumnName("attendee_group_id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(x => x.TerminalAt).HasColumnName("terminal_at");
        builder.Property(x => x.TokenVersion).HasColumnName("token_version");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();

        builder.HasIndex(x => new { x.EventId, x.Email, x.EventGroupId, x.AttendeeGroupId }).IsUnique()
            .HasFilter("status = 1");
        builder.HasIndex(x => new { x.Status, x.ExpiresAt });
        builder.HasIndex(x => new { x.Status, x.TerminalAt });
    }
}
