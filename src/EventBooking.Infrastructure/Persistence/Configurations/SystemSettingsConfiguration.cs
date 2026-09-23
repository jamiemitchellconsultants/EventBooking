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

        builder.HasData(
            new { Id = Domain.Settings.SystemSettings.SingletonId, InviteExpiryDays = 4, MaxAutoRetryCount = 2 });
    }
}
