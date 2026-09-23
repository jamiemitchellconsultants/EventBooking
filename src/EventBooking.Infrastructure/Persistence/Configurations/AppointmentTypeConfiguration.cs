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
        builder.Property(t => t.IsActive).HasColumnName("is_active");
        builder.Property(t => t.Version).HasColumnName("version").IsConcurrencyToken();

        builder.HasIndex(t => t.Code).IsUnique();

        // Types are Admin-managed from Task 5. These three rows are the predecessor's seeded set,
        // kept until Phase 3 moves seeding onto the managed create path.
        builder.HasData(
            new { Id = AppointmentTypeIds.DrugAndAlcoholTesting, Code = "DAT", Name = "Drug & Alcohol Testing", IsActive = true, Version = 1L },
            new { Id = AppointmentTypeIds.MedicalCheckUp, Code = "MED", Name = "Medical Check-up", IsActive = true, Version = 1L },
            new { Id = AppointmentTypeIds.UniformFitting, Code = "UNI", Name = "Uniform Fitting", IsActive = true, Version = 1L });
    }
}
