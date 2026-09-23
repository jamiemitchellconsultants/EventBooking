using EventBooking.Domain.AppointmentTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class AppointmentTypeConfiguration : IEntityTypeConfiguration<AppointmentType>
{
    public void Configure(EntityTypeBuilder<AppointmentType> builder)
    {
        builder.ToTable("appointment_type");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Code).HasColumnName("code").HasMaxLength(8).IsRequired();
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(100).IsRequired();

        builder.HasIndex(t => t.Code).IsUnique();

        // The 3 types are fixed by the spec, so they are seeded rather than created at run time.
        builder.HasData(
            new { Id = AppointmentTypeIds.DrugAndAlcoholTesting, Code = "DAT", Name = "Drug & Alcohol Testing" },
            new { Id = AppointmentTypeIds.MedicalCheckUp, Code = "MED", Name = "Medical Check-up" },
            new { Id = AppointmentTypeIds.UniformFitting, Code = "UNI", Name = "Uniform Fitting" });
    }
}
