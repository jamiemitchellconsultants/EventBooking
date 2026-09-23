using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Settings;
using EventBooking.Domain.Slots;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence;

public sealed class EventBookingDbContext(DbContextOptions<EventBookingDbContext> options)
    : DbContext(options)
{
    public DbSet<AppointmentType> AppointmentTypes => Set<AppointmentType>();

    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();

    public DbSet<SlotProposal> SlotProposals => Set<SlotProposal>();

    public DbSet<ConfirmedSlot> ConfirmedSlots => Set<ConfirmedSlot>();

    public DbSet<SlotCapacity> SlotCapacities => Set<SlotCapacity>();

    /// <summary>Gets Employee Group reference rows and their required Appointment Type mappings.</summary>
    public DbSet<EmployeeGroup> EmployeeGroups => Set<EmployeeGroup>();

    public DbSet<Candidate> Candidates => Set<Candidate>();

    public DbSet<Invite> Invites => Set<Invite>();

    public DbSet<Booking> Bookings => Set<Booking>();

    /// <summary>Gets independently progressing required appointments for persisted bookings.</summary>
    public DbSet<BookingAppointment> BookingAppointments => Set<BookingAppointment>();

    public DbSet<StaffAccessProfile> StaffAccessProfiles => Set<StaffAccessProfile>();

    /// <summary>Gets identity-provider pairs learned from authenticated staff tokens.</summary>
    public DbSet<StaffIdentity> StaffIdentities => Set<StaffIdentity>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventBookingDbContext).Assembly);
    }
}
