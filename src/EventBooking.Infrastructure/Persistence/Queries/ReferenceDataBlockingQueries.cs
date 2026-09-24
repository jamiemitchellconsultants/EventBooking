using EventBooking.Application.ReferenceData;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Events;
using EventBooking.Domain.Locations;
using EventBooking.Domain.AppointmentTypes;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Queries;

public sealed class ReferenceDataBlockingQueries(EventBookingDbContext context) : IReferenceDataBlockingQueries
{
    public async Task<LocationUsage> LocationUsageAsync(Guid locationId, CancellationToken ct) =>
        new(
            await context.EventProposals.CountAsync(
                p => p.Status == EventProposalStatus.Open && p.LocationId == locationId, ct),
            await context.Events.CountAsync(
                e => e.Status == EventStatus.Active
                    && e.LocationId == locationId
                    && EF.Property<DateTimeOffset?>(e, "StartUtc") > DateTimeOffset.UtcNow, ct));

    // Joins go through the explicit join sets (configured in Task 9b), because the
    // listed-type and requirement collections are not EF-translatable projections.
    public async Task<AppointmentTypeUsage> AppointmentTypeUsageAsync(Guid typeId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var openProposals = await (
            from listed in context.Set<EventProposalAppointmentType>()
            join proposal in context.EventProposals on listed.ProposalId equals proposal.Id
            where listed.AppointmentTypeId == typeId && proposal.Status == EventProposalStatus.Open
            select proposal.Id).CountAsync(ct);
        var futureEvents = await (
            from capacity in context.EventCapacities
            join e in context.Events on capacity.EventId equals e.Id
            where capacity.AppointmentTypeId == typeId
                && e.Status == EventStatus.Active
                && EF.Property<DateTimeOffset?>(e, "StartUtc") > now
            select e.Id).Distinct().CountAsync(ct);
        var activeGroups = await (
            from requirement in context.Set<AttendeeGroupRequirement>()
            join g in context.AttendeeGroups on requirement.AttendeeGroupId equals g.Id
            where requirement.AppointmentTypeId == typeId && g.IsActive
            select g.Id).Distinct().CountAsync(ct);
        return new AppointmentTypeUsage(openProposals, futureEvents, activeGroups);
    }

    public Task<int> AttendeeGroupMemberCountAsync(Guid groupId, CancellationToken ct) =>
        context.Attendees.CountAsync(a => a.AttendeeGroupId == groupId, ct);

    public async Task<int> AttendeeGroupBlockingMemberCountAsync(Guid groupId, CancellationToken ct)
    {
        var memberIds = await context.Attendees
            .Where(a => a.AttendeeGroupId == groupId)
            .Select(a => a.Id)
            .ToListAsync(ct);
        return await context.Bookings.CountAsync(
            b => memberIds.Contains(b.AttendeeId)
                && b.Status == BookingStatus.Active
                && b.RecoveryOfBookingId == null, ct);
    }
}
