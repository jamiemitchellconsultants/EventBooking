using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps one Attendee Group to one fixed Appointment Type.</summary>
public sealed class AttendeeGroupRequirementConfiguration : IEntityTypeConfiguration<AttendeeGroupRequirement>
{
    /// <summary>Configures the composite mapping key and restrictive reference keys.</summary>
    public void Configure(EntityTypeBuilder<AttendeeGroupRequirement> builder)
    {
        builder.ToTable("attendee_group_requirement");
        builder.HasKey(requirement => new { requirement.AttendeeGroupId, requirement.AppointmentTypeId });

        builder.Property(requirement => requirement.AttendeeGroupId).HasColumnName("attendee_group_id");
        builder.Property(requirement => requirement.AppointmentTypeId).HasColumnName("appointment_type_id");

        builder.HasOne<AppointmentType>()
            .WithMany()
            .HasForeignKey(requirement => requirement.AppointmentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            Requirement(AttendeeGroupIds.CabinCrew, AppointmentTypeIds.DrugAndAlcoholTesting),
            Requirement(AttendeeGroupIds.CabinCrew, AppointmentTypeIds.MedicalCheckUp),
            Requirement(AttendeeGroupIds.CabinCrew, AppointmentTypeIds.UniformFitting),
            Requirement(AttendeeGroupIds.Pilots, AppointmentTypeIds.DrugAndAlcoholTesting),
            Requirement(AttendeeGroupIds.Pilots, AppointmentTypeIds.UniformFitting),
            Requirement(AttendeeGroupIds.GroundOperationsAgent, AppointmentTypeIds.MedicalCheckUp),
            Requirement(AttendeeGroupIds.Engineering, AppointmentTypeIds.MedicalCheckUp),
            Requirement(AttendeeGroupIds.GroundTransportServices, AppointmentTypeIds.DrugAndAlcoholTesting),
            Requirement(AttendeeGroupIds.GroundTransportServices, AppointmentTypeIds.MedicalCheckUp),
            Requirement(AttendeeGroupIds.GroundTransportServices, AppointmentTypeIds.UniformFitting));
    }

    private static object Requirement(Guid attendeeGroupId, Guid appointmentTypeId) =>
        new { AttendeeGroupId = attendeeGroupId, AppointmentTypeId = appointmentTypeId };
}
