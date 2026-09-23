using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    /// <summary>The index design 04 names for the eligibility query, by its database name.</summary>
    public const string EligibilityIndexName = "ix_event_eligibility";

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
        // The CLR type stays nullable although the column is not: an event that has not been
        // stamped yet has to be distinguishable from one stamped with a default, which is exactly
        // what the save-time backstop looks for. A missing value fails the insert instead of
        // storing 0001-01-01, which the eligibility query would read as an event in the past.
        builder.Property<DateTimeOffset?>(EventStartInstants.PropertyName)
            .HasColumnName("start_utc")
            .IsRequired();

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
            .HasIndex(nameof(Event.Status), nameof(Event.LocationId), EventStartInstants.PropertyName)
            .HasDatabaseName(EligibilityIndexName);
    }
}
