using EventBooking.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class SystemSettingsConfiguration : IEntityTypeConfiguration<SystemSettings>
{
    public void Configure(EntityTypeBuilder<SystemSettings> builder)
    {
        builder.ToTable("system_settings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.InviteExpiryDays).HasColumnName("invite_expiry_days");
        builder.Property(s => s.MaxAutoRetryCount).HasColumnName("max_auto_retry_count");
        builder.Property(s => s.InviteOptionCount).HasColumnName("invite_option_count");
        builder.Property(s => s.PendingRegistrationExpiryHours).HasColumnName("pending_registration_expiry_hours");
        builder.Property(s => s.Version).HasColumnName("version").IsConcurrencyToken();

        builder.HasData(
            new
            {
                Id = Domain.Settings.SystemSettings.SingletonId,
                InviteExpiryDays = 7,
                MaxAutoRetryCount = 2,
                InviteOptionCount = 3,
                PendingRegistrationExpiryHours = 48,
                Version = 1L,
            });
    }
}
