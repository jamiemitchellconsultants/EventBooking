using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class EventCapacityConfiguration : IEntityTypeConfiguration<EventCapacity>
{
    public void Configure(EntityTypeBuilder<EventCapacity> builder)
    {
        // The table and column names here are written out in raw SQL in Task 47. Changing either
        // one means changing that query in the same commit.
        builder.ToTable("event_capacity");

        builder.HasKey(c => new { c.EventId, c.AppointmentTypeId });

        builder.Property(c => c.EventId).HasColumnName("event_id");
        builder.Property(c => c.AppointmentTypeId).HasColumnName("appointment_type_id");
        builder.Property(c => c.TotalHeadcount).HasColumnName("total_headcount");
        builder.Property(c => c.RemainingCapacity).HasColumnName("remaining_capacity");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_event_capacity_bounds",
            "remaining_capacity >= 0 AND remaining_capacity <= total_headcount AND total_headcount > 0"));
    }
}
