using EventBooking.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class EmailLogConfiguration : IEntityTypeConfiguration<EmailLog>
{
    public void Configure(EntityTypeBuilder<EmailLog> builder)
    {
        builder.ToTable("email_log");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.AttendeeId).HasColumnName("attendee_id");
        builder.Property(e => e.SelfRegistrationId).HasColumnName("self_registration_id");
        builder.Property(e => e.TemplateName).HasColumnName("template_name").HasConversion<int>();
        builder.Property(e => e.SentAt).HasColumnName("sent_at");
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(e => e.InviteId).HasColumnName("invite_id");
        builder.Property(e => e.BookingId).HasColumnName("booking_id");
        builder.Property(e => e.EventId).HasColumnName("event_id");
        builder.Property(e => e.ClaimedAt).HasColumnName("claimed_at");
        builder.Property(e => e.ClaimCount).HasColumnName("claim_count");
        builder.Property(e => e.NotBefore).HasColumnName("not_before");
        builder.Property(e => e.CorrelationId).HasColumnName("correlation_id");

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_email_log_one_recipient_context",
            "(attendee_id IS NULL) <> (self_registration_id IS NULL)"));
        builder.HasIndex(e => e.SelfRegistrationId);
        builder.HasIndex(e => e.AttendeeId);
        builder.HasIndex(e => new { e.AttendeeId, e.SentAt });
    }
}
