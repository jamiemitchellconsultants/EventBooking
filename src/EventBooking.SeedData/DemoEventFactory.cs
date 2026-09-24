using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Application.Events;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;
using EventBooking.Domain.Locations;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Persistence;

namespace EventBooking.SeedData;

/// <summary>Reconstructs accepted demo proposals instead of creating events without negotiation history.</summary>
internal static class DemoEventFactory
{
    private static readonly IEventWindowZones zones = new Infrastructure.Time.NodaTimeEventWindowZones();

    internal static Event Create(EventBookingDbContext database, Guid id, EventWindow window,
        IReadOnlyDictionary<Guid, int> headcounts, DateTimeOffset timestamp)
    {
        var managers = DemoSeedSpec.Staff().Where(staff => staff.Roles.Contains(Role.Manager))
            .ToDictionary(staff => staff.AppointmentTypeId!.Value, staff => staff.UserId);
        var types = AppointmentTypeIds.All.Order().ToArray();
        var proposal = EventProposal.Propose(
            Guid.NewGuid(),
            TransitionalLocation.Id,
            locationIsActive: true,
            TransitionalLocation.TimeZoneId,
            window,
            zones,
            window.StartInstant(zones, TransitionalLocation.TimeZoneId).AddDays(-1),
            [.. types.Select(type => new ProposableAppointmentType(
                type, AppointmentTypeIds.CodeOf(type), true, true))],
            types[0],
            managers[types[0]],
            headcounts[types[0]]);
        foreach (var type in types)
            proposal.Accept(type, managers[type], headcounts[type]);
        var created = Event.CreateFrom(id, proposal);
        database.EventProposals.Add(proposal);
        database.Events.Add(created);
        database.AuditLogs.Add(AuditLog.Record(
            Guid.NewGuid(), AuditEntityTypes.EventProposal, proposal.Id,
            AuditAction.ProposalCreated, ActorType.Staff, managers[types[0]].ToString(),
            timestamp, window.ToString()));
        foreach (var type in types)
            database.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.EventProposal, proposal.Id,
                AuditAction.AcceptanceRecorded, ActorType.Staff, managers[type].ToString(),
                timestamp, $"{AppointmentTypeIds.NameOf(type)} headcount {headcounts[type]}"));
        database.AuditLogs.Add(AuditLog.Record(
            Guid.NewGuid(), AuditEntityTypes.Event, created.Id,
            AuditAction.EventConfirmed, ActorType.Staff, managers[types[^1]].ToString(),
            timestamp, created.Window.ToString()));
        return created;
    }
}
