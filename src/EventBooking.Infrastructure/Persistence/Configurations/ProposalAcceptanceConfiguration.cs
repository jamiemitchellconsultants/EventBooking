using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class ProposalAcceptanceConfiguration : IEntityTypeConfiguration<ProposalAcceptance>
{
    public void Configure(EntityTypeBuilder<ProposalAcceptance> builder)
    {
        builder.ToTable("proposal_acceptance");

        // One acceptance per appointment type per proposal — the composite key is the invariant.
        builder.HasKey(a => new { a.ProposalId, a.AppointmentTypeId });

        builder.Property(a => a.ProposalId).HasColumnName("proposal_id");
        builder.Property(a => a.AppointmentTypeId).HasColumnName("appointment_type_id");
        builder.Property(a => a.ManagerUserId).HasColumnName("manager_user_id");
        builder.Property(a => a.Headcount).HasColumnName("headcount");
    }
}
