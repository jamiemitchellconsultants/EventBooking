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
    }
}
