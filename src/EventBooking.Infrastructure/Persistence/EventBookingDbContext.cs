using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Locations;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Settings;
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence;

public sealed class EventBookingDbContext(
    DbContextOptions<EventBookingDbContext> options,
    ICorrelationContext? correlation = null)
    : DbContext(options)
{
    // The IANA rules are versioned data, not configuration, and the resolver is a pure function
    // over them, so the context holds one rather than taking it as a dependency every caller that
    // builds a context by hand would then have to supply.
    private static readonly IEventWindowZones Zones = new NodaTimeEventWindowZones();

    public DbSet<AppointmentType> AppointmentTypes => Set<AppointmentType>();

    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();

    public DbSet<EventProposal> EventProposals => Set<EventProposal>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<EventCapacity> EventCapacities => Set<EventCapacity>();

    /// <summary>Gets Attendee Group reference rows and their required Appointment Type mappings.</summary>
    public DbSet<Location> Locations => Set<Location>();

    public DbSet<AttendeeGroup> AttendeeGroups => Set<AttendeeGroup>();

    public DbSet<Attendee> Attendees => Set<Attendee>();

    public DbSet<Invite> Invites => Set<Invite>();

    public DbSet<Booking> Bookings => Set<Booking>();

    /// <summary>Gets independently progressing required appointments for persisted bookings.</summary>
    public DbSet<BookingAppointment> BookingAppointments => Set<BookingAppointment>();

    public DbSet<StaffAccessProfile> StaffAccessProfiles => Set<StaffAccessProfile>();

    /// <summary>Gets identity-provider pairs learned from authenticated staff tokens.</summary>
    public DbSet<StaffIdentity> StaffIdentities => Set<StaffIdentity>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();

    /// <summary>Gets the Idempotency-Key retention rows.</summary>
    public DbSet<Idempotency.IdempotencyRecord> IdempotencyRecords => Set<Idempotency.IdempotencyRecord>();

    /// <summary>
    /// Fills in the derived start instant for any event being inserted that has not had one
    /// computed, so the column the eligibility query filters and orders on cannot be left empty by
    /// a writer that has not heard of the rule.
    /// </summary>
    /// <param name="acceptAllChangesOnSuccess">Whether to accept the tracked changes on success.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        await EventStartInstants.StampPendingAsync(this, Zones, cancellationToken);

        // The same shape as the start-instant backstop, and for the same reason: an outbox
        // row is staged from a handful of handlers plus the seeder, and a derived column cannot
        // depend on which path inserted the row. The stamp is write-once, so a row that already
        // names its correlation keeps it. A null context means no stamp, which is the right
        // answer outside the host — a row written by a test has no request to correlate with.
        if (correlation is not null)
        {
            foreach (var entry in ChangeTracker.Entries<EmailLog>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.StampCorrelation(correlation.CorrelationId);
                }
            }
        }

        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventBookingDbContext).Assembly);
    }
}
