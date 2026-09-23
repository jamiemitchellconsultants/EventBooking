using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class AttendeeConfiguration : IEntityTypeConfiguration<Attendee>
{
    public void Configure(EntityTypeBuilder<Attendee> builder)
    {
        builder.ToTable("attendee");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(c => c.AttendeeGroupId).HasColumnName("attendee_group_id").IsRequired();
        builder.HasOne<AttendeeGroup>()
            .WithMany()
            .HasForeignKey(c => c.AttendeeGroupId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => c.AttendeeGroupId);
        builder.Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty)
            .HasColumnName("status_changed_at");

        builder.Ignore(c => c.RequiredAppointmentTypeIds);

        builder
            .HasMany(c => c.Requirements)
            .WithOne()
            .HasForeignKey(r => r.AttendeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(c => c.Email).IsUnique();
        builder.HasIndex(c => c.Status);
    }
}
