using EventBooking.Domain.Invites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class InviteOptionConfiguration : IEntityTypeConfiguration<InviteOption>
{
    public void Configure(EntityTypeBuilder<InviteOption> builder)
    {
        builder.ToTable("invite_option");
        builder.HasKey(o => new { o.InviteId, o.ConfirmedSlotId });

        builder.Property(o => o.InviteId).HasColumnName("invite_id");
        builder.Property(o => o.ConfirmedSlotId).HasColumnName("confirmed_slot_id");
    }
}
