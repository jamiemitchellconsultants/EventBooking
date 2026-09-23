using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps booking-appointment identity, lifecycle fields, constraints, and indexes.</summary>
public sealed class BookingAppointmentConfiguration : IEntityTypeConfiguration<BookingAppointment>
{
    /// <summary>Configures booking-appointment columns, constraints, and indexes.</summary>
    /// <param name="builder">The entity-type builder.</param>
    public void Configure(EntityTypeBuilder<BookingAppointment> builder)
    {
        builder.ToTable("booking_appointment", table =>
        {
            table.HasCheckConstraint("CK_booking_appointment_version", "version > 0");
            table.HasCheckConstraint(
                "CK_booking_appointment_last_change_pair",
                "(last_changed_by_staff_user_id IS NULL) = (last_changed_at IS NULL)");
            table.HasCheckConstraint(
                "CK_booking_appointment_status_timestamps",
                """
                (status = 1 AND checked_in_at IS NULL AND outcome_at IS NULL)
                OR (status = 2 AND checked_in_at IS NOT NULL AND outcome_at IS NULL)
                OR (status = 3 AND checked_in_at IS NOT NULL AND outcome_at IS NOT NULL
                    AND outcome_at >= checked_in_at)
                OR (status = 4 AND checked_in_at IS NULL AND outcome_at IS NOT NULL)
                """);
        });

        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id");
        builder.Property(value => value.BookingId).HasColumnName("booking_id");
        builder.Property(value => value.AppointmentTypeId).HasColumnName("appointment_type_id");
        builder.Property(value => value.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(value => value.CheckedInAt).HasColumnName("checked_in_at");
        builder.Property(value => value.OutcomeAt).HasColumnName("outcome_at");
        builder.Property(value => value.LastChangedByStaffUserId)
            .HasColumnName("last_changed_by_staff_user_id");
        builder.Property(value => value.LastChangedAt).HasColumnName("last_changed_at");
        builder.Property(value => value.Version).HasColumnName("version").IsConcurrencyToken();

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(value => value.BookingId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppointmentType>()
            .WithMany()
            .HasForeignKey(value => value.AppointmentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(value => new { value.BookingId, value.AppointmentTypeId }).IsUnique();
        builder.HasIndex(value => new { value.AppointmentTypeId, value.Status });
    }
}
