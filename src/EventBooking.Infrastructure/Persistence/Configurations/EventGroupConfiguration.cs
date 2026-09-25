using EventBooking.Domain.EventGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps the Event Group publication aggregate.</summary>
public sealed class EventGroupConfiguration : IEntityTypeConfiguration<EventGroup>
{
    /// <summary>Configures the event group table, constraints, and membership collections.</summary>
    public void Configure(EntityTypeBuilder<EventGroup> builder)
    {
        builder.ToTable("event_group", table =>
        {
            table.HasCheckConstraint("ck_event_group_title_nonblank", "title <> ''");
        });
        builder.HasKey(group => group.Id);

        builder.Property(group => group.Id).HasColumnName("id");
        builder.Property(group => group.Title).HasColumnName("title").HasMaxLength(160).IsRequired();
        builder.Property(group => group.Description).HasColumnName("description").HasMaxLength(2000).IsRequired();
        builder.Property(group => group.IsOpen).HasColumnName("is_open");
        builder.Property(group => group.Version).HasColumnName("version").IsConcurrencyToken();

        builder
            .HasMany(group => group.AttendeeGroups)
            .WithOne()
            .HasForeignKey(selected => selected.EventGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(group => group.AttendeeGroups).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder
            .HasMany(group => group.Events)
            .WithOne()
            .HasForeignKey(membership => membership.EventGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(group => group.Events).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
