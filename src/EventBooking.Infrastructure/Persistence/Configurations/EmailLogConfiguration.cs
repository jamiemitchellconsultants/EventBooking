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
        builder.Property(e => e.CandidateId).HasColumnName("candidate_id");
        builder.Property(e => e.TemplateName).HasColumnName("template_name").HasConversion<int>();
        builder.Property(e => e.SentAt).HasColumnName("sent_at");
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(e => e.InviteId).HasColumnName("invite_id");
        builder.Property(e => e.BookingId).HasColumnName("booking_id");
        builder.Property(e => e.ConfirmedSlotId).HasColumnName("confirmed_slot_id");
        builder.Property(e => e.ClaimedAt).HasColumnName("claimed_at");

        builder.HasIndex(e => e.CandidateId);
        builder.HasIndex(e => new { e.CandidateId, e.SentAt });
    }
}
