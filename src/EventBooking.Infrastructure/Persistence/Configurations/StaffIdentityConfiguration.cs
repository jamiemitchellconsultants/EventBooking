using EventBooking.Domain.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps the identity-provider mirror and its format and uniqueness backstops.</summary>
public sealed class StaffIdentityConfiguration : IEntityTypeConfiguration<StaffIdentity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<StaffIdentity> builder)
    {
        builder.ToTable("staff_identity", table => table.HasCheckConstraint(
            "ck_staff_identity_format",
            "staff_id ~* '^[UN][0-9]{6}$'"));

        builder.HasKey(identity => identity.StaffUserId);
        builder.Property(identity => identity.StaffUserId)
            .HasColumnName("staff_user_id")
            .ValueGeneratedNever();
        builder.Property(identity => identity.StaffId)
            .HasConversion(staffId => staffId.Value, value => new StaffId(value))
            .HasColumnName("staff_id")
            .HasColumnType("character(7)")
            .IsRequired();
        builder.Property(identity => identity.LastSeenAt)
            .HasColumnName("last_seen_at")
            .IsRequired();
        // No check constraint: unlike the staff number, a provider-observed name has no format.
        builder.Property(identity => identity.DisplayName)
            .HasColumnName("display_name")
            .HasColumnType("text")
            .IsRequired(false);

        builder.HasIndex(identity => identity.StaffId)
            .IsUnique()
            .HasDatabaseName("ux_staff_identity_staff_id");
    }
}
