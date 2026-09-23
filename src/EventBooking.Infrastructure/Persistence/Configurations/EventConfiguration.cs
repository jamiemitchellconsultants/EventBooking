using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("event");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.ProposalId).HasColumnName("proposal_id").IsRequired();
        builder.Property(s => s.LocationId).HasColumnName("location_id");
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>();

        // A derived persistence column, not domain data (design 04). It exists to index and order
        // the eligibility query, and the domain never reads it as the source of truth; PostgreSQL
        // cannot evaluate IANA rules in a generated column, so the application computes it.
        //
        // Nullable until Task 11, which is where the repository writes it in the same transaction
        // as the insert and makes the column required. A non-nullable column here would take EF's
        // default of 0001-01-01 for every row nothing has computed yet, and the eligibility query
        // filters on start_utc: a wrong instant would quietly hide the event rather than fail.
        builder.Property<DateTimeOffset?>("StartUtc").HasColumnName("start_utc");

        builder.OwnsOne(s => s.Window, window =>
        {
            window.Property(w => w.Date).HasColumnName("date");
            window.Property(w => w.StartTime).HasColumnName("start_time");
            window.Property(w => w.DurationMinutes).HasColumnName("duration_minutes");
            window.Ignore(w => w.EndTime);
        });
        builder.Navigation(s => s.Window).IsRequired();

        builder
            .HasMany(s => s.Capacities)
            .WithOne()
            .HasForeignKey(c => c.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Capacities).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => s.ProposalId).IsUnique();
        builder.HasIndex(s => s.Status);
        builder
            .HasIndex(nameof(Event.Status), nameof(Event.LocationId), "StartUtc")
            .HasDatabaseName("ix_event_eligibility");
    }
}
