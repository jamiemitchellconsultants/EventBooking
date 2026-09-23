using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Invites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps one snapshotted Appointment Type owned by an Invite.</summary>
public sealed class InviteRequirementConfiguration : IEntityTypeConfiguration<InviteRequirement>
{
    /// <summary>Configures the composite snapshot key and restrictive reference keys.</summary>
    public void Configure(EntityTypeBuilder<InviteRequirement> builder)
    {
        builder.ToTable("invite_requirement");
        builder.HasKey(requirement => new { requirement.InviteId, requirement.AppointmentTypeId });

        builder.Property(requirement => requirement.InviteId).HasColumnName("invite_id");
        builder.Property(requirement => requirement.AppointmentTypeId).HasColumnName("appointment_type_id");

        builder.HasOne<AppointmentType>()
            .WithMany()
            .HasForeignKey(requirement => requirement.AppointmentTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
