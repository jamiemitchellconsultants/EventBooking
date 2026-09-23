using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.EmployeeGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps one Employee Group to one fixed Appointment Type.</summary>
public sealed class EmployeeGroupRequirementConfiguration : IEntityTypeConfiguration<EmployeeGroupRequirement>
{
    /// <summary>Configures the composite mapping key and restrictive reference keys.</summary>
    public void Configure(EntityTypeBuilder<EmployeeGroupRequirement> builder)
    {
        builder.ToTable("employee_group_requirement");
        builder.HasKey(requirement => new { requirement.EmployeeGroupId, requirement.AppointmentTypeId });

        builder.Property(requirement => requirement.EmployeeGroupId).HasColumnName("employee_group_id");
        builder.Property(requirement => requirement.AppointmentTypeId).HasColumnName("appointment_type_id");

        builder.HasOne<AppointmentType>()
            .WithMany()
            .HasForeignKey(requirement => requirement.AppointmentTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
