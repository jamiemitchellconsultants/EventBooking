using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.EventGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps one Attendee Group selected by one Event Group.</summary>
public sealed class EventGroupAttendeeGroupConfiguration : IEntityTypeConfiguration<EventGroupAttendeeGroup>
{
    /// <summary>Configures the composite selection key and restrictive reference keys.</summary>
    public void Configure(EntityTypeBuilder<EventGroupAttendeeGroup> builder)
    {
        builder.ToTable("event_group_attendee_group");
        builder.HasKey(x => new { x.EventGroupId, x.AttendeeGroupId });

        builder.Property(x => x.EventGroupId).HasColumnName("event_group_id");
        builder.Property(x => x.AttendeeGroupId).HasColumnName("attendee_group_id");

        builder.HasOne<AttendeeGroup>()
            .WithMany()
            .HasForeignKey(x => x.AttendeeGroupId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
