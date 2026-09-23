using EventBooking.Domain.Invites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps one Location an Invite's options may be drawn from.</summary>
public sealed class InviteLocationConfiguration : IEntityTypeConfiguration<InviteLocation>
{
    /// <summary>
    /// Configures the composite key. There is deliberately no foreign key to the location table:
    /// the Location aggregate Task 5 introduced is not persisted until Task 9's fresh schema, and
    /// mapping it from here would have this migration create a second, parallel table for it.
    /// </summary>
    public void Configure(EntityTypeBuilder<InviteLocation> builder)
    {
        builder.ToTable("invite_location");
        builder.HasKey(location => new { location.InviteId, location.LocationId });

        builder.Property(location => location.InviteId).HasColumnName("invite_id");
        builder.Property(location => location.LocationId).HasColumnName("location_id");

        builder.HasIndex(location => location.LocationId);
    }
}
