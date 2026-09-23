using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps proposal persistence and its one-open-window uniqueness backstop.</summary>
public sealed class EventProposalConfiguration : IEntityTypeConfiguration<EventProposal>
{
    /// <summary>Configures proposal columns, acceptances, and the filtered window index.</summary>
    public void Configure(EntityTypeBuilder<EventProposal> builder)
    {
        builder.ToTable("event_proposal");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(p => p.CreatedByManagerUserId).HasColumnName("created_by_manager_user_id");

        // The 4-hour window lives in this table's own date and start_time columns.
        builder.OwnsOne(p => p.Window, window =>
        {
            window.Property(w => w.Date).HasColumnName("date");
            window.Property(w => w.StartTime).HasColumnName("start_time");
            window.Property(w => w.DurationMinutes).HasColumnName("duration_minutes");
            window.Ignore(w => w.EndTime);
            window.HasIndex(item => new { item.Date, item.StartTime })
                .HasDatabaseName("ux_event_proposal_open_window")
                .HasFilter("status = 1")
                .IsUnique();
        });
        builder.Navigation(p => p.Window).IsRequired();

        builder
            .HasMany(p => p.Acceptances)
            .WithOne()
            .HasForeignKey(a => a.ProposalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Acceptances).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(p => p.Status);

    }
}
