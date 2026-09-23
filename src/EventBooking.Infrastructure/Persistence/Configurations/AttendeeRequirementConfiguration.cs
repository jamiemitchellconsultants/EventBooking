using EventBooking.Domain.Attendees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class AttendeeRequirementConfiguration : IEntityTypeConfiguration<AttendeeRequirement>
{
    public void Configure(EntityTypeBuilder<AttendeeRequirement> builder)
    {
        builder.ToTable("attendee_requirement");
        builder.HasKey(r => new { r.AttendeeId, r.AppointmentTypeId });

        builder.Property(r => r.AttendeeId).HasColumnName("attendee_id");
        builder.Property(r => r.AppointmentTypeId).HasColumnName("appointment_type_id");
    }
}
