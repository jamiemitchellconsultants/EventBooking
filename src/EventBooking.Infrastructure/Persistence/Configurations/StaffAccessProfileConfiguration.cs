using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class StaffAccessProfileConfiguration : IEntityTypeConfiguration<StaffAccessProfile>
{
    public void Configure(EntityTypeBuilder<StaffAccessProfile> builder)
    {
        builder.ToTable("staff_access_profile", table =>
        {
            table.HasCheckConstraint(
                "ck_staff_access_profile_has_role",
                "is_admin OR is_coordinator OR is_manager OR is_appointment_staff");
            table.HasCheckConstraint(
                "ck_staff_access_profile_admin_exclusive",
                "NOT is_admin OR (NOT is_coordinator AND NOT is_manager "
                + "AND NOT is_appointment_staff AND appointment_type_id IS NULL)");
            table.HasCheckConstraint(
                "ck_staff_access_profile_scope",
                "appointment_type_id IS NULL OR (is_manager OR is_appointment_staff)");
            table.HasCheckConstraint("ck_staff_access_profile_version", "version > 0");
        });

        builder.HasKey(profile => profile.StaffUserId);
        builder.Property(profile => profile.StaffUserId)
            .HasColumnName("staff_user_id")
            .ValueGeneratedNever();
        builder.Property(profile => profile.IsAdmin).HasColumnName("is_admin");
        builder.Property(profile => profile.IsCoordinator).HasColumnName("is_coordinator");
        builder.Property(profile => profile.IsManager).HasColumnName("is_manager");
        builder.Property(profile => profile.IsAppointmentStaff).HasColumnName("is_appointment_staff");
        builder.Property(profile => profile.AppointmentTypeId).HasColumnName("appointment_type_id");
        builder.Property(profile => profile.Version)
            .HasColumnName("version")
            .IsConcurrencyToken();

        builder.Ignore(profile => profile.Roles);
        builder.HasOne<AppointmentType>()
            .WithMany()
            .HasForeignKey(profile => profile.AppointmentTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(profile => profile.AppointmentTypeId)
            .IsUnique()
            .HasFilter("is_manager")
            .HasDatabaseName("ux_staff_access_profile_manager_appointment_type");
    }
}
