using EventBooking.Domain.Slots;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class SlotCapacityConfiguration : IEntityTypeConfiguration<SlotCapacity>
{
    public void Configure(EntityTypeBuilder<SlotCapacity> builder)
    {
        // The table and column names here are written out in raw SQL in Task 47. Changing either
        // one means changing that query in the same commit.
        builder.ToTable("slot_capacity");

        builder.HasKey(c => new { c.ConfirmedSlotId, c.AppointmentTypeId });

        builder.Property(c => c.ConfirmedSlotId).HasColumnName("confirmed_slot_id");
        builder.Property(c => c.AppointmentTypeId).HasColumnName("appointment_type_id");
        builder.Property(c => c.TotalHeadcount).HasColumnName("total_headcount");
        builder.Property(c => c.RemainingCapacity).HasColumnName("remaining_capacity");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_slot_capacity_within_bounds",
            "remaining_capacity >= 0 AND remaining_capacity <= total_headcount"));
    }
}
