using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps the fixed list of appointment types one proposal offers.</summary>
public sealed class EventProposalAppointmentTypeConfiguration
    : IEntityTypeConfiguration<EventProposalAppointmentType>
{
    /// <summary>Configures the listed-type rows and their composite key.</summary>
    public void Configure(EntityTypeBuilder<EventProposalAppointmentType> builder)
    {
        builder.ToTable("event_proposal_appointment_type");
        builder.HasKey(listed => new { listed.ProposalId, listed.AppointmentTypeId });

        builder.Property(listed => listed.ProposalId).HasColumnName("proposal_id");
        builder.Property(listed => listed.AppointmentTypeId).HasColumnName("appointment_type_id");
    }
}
