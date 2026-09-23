using EventBooking.Domain.Candidates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class CandidateRequirementConfiguration : IEntityTypeConfiguration<CandidateRequirement>
{
    public void Configure(EntityTypeBuilder<CandidateRequirement> builder)
    {
        builder.ToTable("candidate_requirement");
        builder.HasKey(r => new { r.CandidateId, r.AppointmentTypeId });

        builder.Property(r => r.CandidateId).HasColumnName("candidate_id");
        builder.Property(r => r.AppointmentTypeId).HasColumnName("appointment_type_id");
    }
}
