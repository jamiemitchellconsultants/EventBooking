using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps invite persistence and its one-pending-invite-per-attendee backstop.</summary>
public sealed class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    /// <summary>Configures invite columns, options, and the filtered pending-invite index.</summary>
    public void Configure(EntityTypeBuilder<Invite> builder)
    {
        builder.ToTable("invite");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.AttendeeId).HasColumnName("attendee_id");
        builder.Property(i => i.TokenHash).HasColumnName("token_hash").HasMaxLength(200).IsRequired();
        builder.Property(i => i.ExpiresAt).HasColumnName("expires_at");
        builder.Property(i => i.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(i => i.RetryCount).HasColumnName("retry_count");
        builder.Property(i => i.RecoveryOfBookingId).HasColumnName("recovery_of_booking_id");

        builder.Ignore(i => i.OfferedEventIds);
        builder.Ignore(i => i.RequiredAppointmentTypeIds);

        builder
            .HasMany(i => i.Options)
            .WithOne()
            .HasForeignKey(o => o.InviteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.Options).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder
            .HasMany(i => i.Requirements)
            .WithOne()
            .HasForeignKey(r => r.InviteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(i => i.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(i => i.RecoveryOfBookingId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => i.RecoveryOfBookingId);

        builder.HasIndex(i => i.TokenHash).IsUnique();
        builder.HasIndex(i => new { i.Status, i.ExpiresAt });
        builder.HasIndex(i => i.AttendeeId);
        builder.HasIndex(i => i.AttendeeId)
            .HasDatabaseName("ux_invite_pending_attendee")
            .HasFilter("status = 1")
            .IsUnique();
    }
}
