using EventBooking.Domain.EventGroups;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps one Event membership in one Event Group with its own publication gate.</summary>
public sealed class EventGroupEventConfiguration : IEntityTypeConfiguration<EventGroupEvent>
{
    /// <summary>Configures the composite membership key, the open-membership index, and restrictive reference keys.</summary>
    public void Configure(EntityTypeBuilder<EventGroupEvent> builder)
    {
        builder.ToTable("event_group_event");
        builder.HasKey(x => new { x.EventGroupId, x.EventId });

        builder.Property(x => x.EventGroupId).HasColumnName("event_group_id");
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.IsOpen).HasColumnName("is_open");

        builder.HasIndex(x => new { x.EventGroupId, x.IsOpen }).HasFilter("is_open");

        builder.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
