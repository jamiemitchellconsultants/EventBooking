using EventBooking.Application.Events;
using EventBooking.Domain.Locations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("location");
        builder.HasKey(location => location.Id);

        builder.Property(location => location.Id).HasColumnName("id");
        builder.Property(location => location.Code)
            .HasColumnName("code")
            .HasMaxLength(Location.MaximumCodeLength)
            .IsRequired();
        builder.Property(location => location.Name)
            .HasColumnName("name")
            .HasMaxLength(Location.MaximumNameLength)
            .IsRequired();
        builder.Property(location => location.Address)
            .HasColumnName("address")
            .HasMaxLength(Location.MaximumAddressLength)
            .IsRequired();

        // The IANA identifier, not an offset: an offset cannot answer "was this in summer time".
        builder.Property(location => location.TimeZoneId)
            .HasColumnName("time_zone_id")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(location => location.IsActive).HasColumnName("is_active");
        builder.Property(location => location.Version).HasColumnName("version").IsConcurrencyToken();

        builder.HasIndex(location => location.Code).IsUnique();
    }
}
