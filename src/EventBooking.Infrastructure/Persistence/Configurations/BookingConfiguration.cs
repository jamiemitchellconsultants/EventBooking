using EventBooking.Domain.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps booking persistence and its one-active-booking-per-candidate backstop.</summary>
public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    /// <summary>Configures booking columns and the filtered active-booking index.</summary>
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable(
            "booking",
            table => table.HasCheckConstraint(
                "ck_booking_no_self_recovery",
                "recovery_of_booking_id IS NULL OR recovery_of_booking_id <> id"));
        builder.HasKey(b => b.Id);
        builder.Ignore(b => b.IsOriginal);

        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.CandidateId).HasColumnName("candidate_id");
        builder.Property(b => b.ConfirmedSlotId).HasColumnName("confirmed_slot_id");
        builder.Property(b => b.InviteId).HasColumnName("invite_id");
        builder.Property(b => b.CreatedAt).HasColumnName("created_at");
        builder.Property(b => b.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(b => b.RecoveryOfBookingId).HasColumnName("recovery_of_booking_id");
        builder
            .Property(b => b.ManageTokenHash)
            .HasColumnName("manage_token_hash")
            .HasMaxLength(200)
            .IsRequired();

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(b => b.RecoveryOfBookingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.ManageTokenHash).IsUnique();
        builder.HasIndex(b => new { b.ConfirmedSlotId, b.Status });
        builder.HasIndex(b => new { b.CandidateId, b.Status });
        builder.HasIndex(b => b.RecoveryOfBookingId);
        builder.HasIndex(b => b.CandidateId)
            .HasDatabaseName("ux_booking_active_original_candidate")
            .HasFilter("status = 1 AND recovery_of_booking_id IS NULL")
            .IsUnique();
        builder.HasIndex(b => b.RecoveryOfBookingId)
            .HasDatabaseName("ux_booking_active_recovery")
            .HasFilter("status = 1 AND recovery_of_booking_id IS NOT NULL")
            .IsUnique();
    }
}
