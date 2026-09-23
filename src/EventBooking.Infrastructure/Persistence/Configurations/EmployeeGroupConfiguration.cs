using EventBooking.Domain.EmployeeGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps the change-controlled Employee Group reference table.</summary>
public sealed class EmployeeGroupConfiguration : IEntityTypeConfiguration<EmployeeGroup>
{
    /// <summary>Configures the employee group table, constraints, and mapping collection.</summary>
    public void Configure(EntityTypeBuilder<EmployeeGroup> builder)
    {
        builder.ToTable("employee_group", table =>
        {
            table.HasCheckConstraint("ck_employee_group_code_nonblank", "code <> ''");
            table.HasCheckConstraint("ck_employee_group_name_nonblank", "name <> ''");
        });
        builder.HasKey(group => group.Id);

        builder.Property(group => group.Id).HasColumnName("id");
        builder.Property(group => group.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        builder.Property(group => group.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(group => group.IsActive).HasColumnName("is_active");

        builder.Ignore(group => group.RequiredAppointmentTypeIds);

        builder
            .HasMany(group => group.Requirements)
            .WithOne()
            .HasForeignKey(requirement => requirement.EmployeeGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(group => group.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(group => group.Code).IsUnique();
    }
}
