using EventBooking.Domain.AttendeeGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps the change-controlled Attendee Group reference table.</summary>
public sealed class AttendeeGroupConfiguration : IEntityTypeConfiguration<AttendeeGroup>
{
    /// <summary>Configures the attendee group table, constraints, and mapping collection.</summary>
    public void Configure(EntityTypeBuilder<AttendeeGroup> builder)
    {
        builder.ToTable("attendee_group", table =>
        {
            table.HasCheckConstraint("ck_attendee_group_code_nonblank", "code <> ''");
            table.HasCheckConstraint("ck_attendee_group_name_nonblank", "name <> ''");
        });
        builder.HasKey(group => group.Id);

        builder.Property(group => group.Id).HasColumnName("id");
        builder.Property(group => group.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        builder.Property(group => group.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(group => group.IsActive).HasColumnName("is_active");
        builder.Property(group => group.Version).HasColumnName("version").IsConcurrencyToken();

        builder.Ignore(group => group.RequiredAppointmentTypeIds);

        builder
            .HasMany(group => group.Requirements)
            .WithOne()
            .HasForeignKey(requirement => requirement.AttendeeGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(group => group.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(group => group.Code).IsUnique();

        // Change-controlled reference data the predecessor seeded from a migration, kept here so
        // the fresh schema carries it without a second release. Retires with the fixed appointment
        // types in Phase 3.
        builder.HasData(
            new { Id = AttendeeGroupIds.CabinCrew, Code = "CABIN_CREW", Name = "Cabin Crew", IsActive = true, Version = 1L },
            new { Id = AttendeeGroupIds.Pilots, Code = "PILOTS", Name = "Pilots", IsActive = true, Version = 1L },
            new { Id = AttendeeGroupIds.GroundOperationsAgent, Code = "GROUND_OPERATIONS_AGENT", Name = "Ground Operations Agent", IsActive = true, Version = 1L },
            new { Id = AttendeeGroupIds.Engineering, Code = "ENGINEERING", Name = "Engineering", IsActive = true, Version = 1L },
            new { Id = AttendeeGroupIds.GroundTransportServices, Code = "GROUND_TRANSPORT_SERVICES", Name = "Ground Transport Services", IsActive = true, Version = 1L });
    }
}
