using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Domain.Locations;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies portal and confirmation treat Invite Requirements as immutable authority.</summary>
public sealed class InviteSnapshotAuthorityTests
{
    /// <summary>The portal displays snapshot names rather than a later Attendee collection.</summary>
    [Fact]
    public async Task ViewInviteUsesPersistedSnapshot()
    {
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group, ProposalFixture.Now);
        var attendees = new InMemoryAttendeeRepository();
        attendees.Add(attendee);
        var events = new InMemoryEventRepository();
        var options = Enumerable.Range(0, 3).Select(index =>
            EventFixture.Create(
                Guid.NewGuid(), new EventWindow(new DateOnly(2026, 10, 10 + index), new TimeOnly(9, 0), 240),
                new Dictionary<Guid, int>
                {
                    [AppointmentTypeIds.DrugAndAlcoholTesting] = 5,
                    [AppointmentTypeIds.MedicalCheckUp] = 5,
                    [AppointmentTypeIds.UniformFitting] = 5,
                })).ToList();
        events.Items.AddRange(options);
        var tokens = new FakeTokenService();
        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(TokenPurpose.Book, inviteId, Invite.InitialTokenVersion);
        var invite = Invite.CreateInitial(
            inviteId,
            attendee.Id,
            DateTimeOffset.Parse("2026-10-01T00:00:00Z"),
            [ProposalFixture.LocationId],
            options.Select(eventItem => eventItem.Id),
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        var invites = new InMemoryInviteRepository();
        invites.Add(invite);
        var clock = new FakeClock(DateTimeOffset.Parse("2026-09-20T00:00:00Z"));
        var locations = new InMemoryLocationRepository();
        locations.Items.Add(Location.Create(
            ProposalFixture.LocationId, "LONDON_HQ", "London HQ", "1 High St",
            "Europe/London", ProposalFixture.Zones));

        var result = await new ViewInviteHandler(
                invites, attendees, events, new EligibleEventFinder(events, events, clock),
                new RecordingAuditLogger(), new FakeUnitOfWork(), tokens, clock,
                locations, new InMemoryAppointmentTypeRepository())
            .HandleAsync(new ViewInviteQuery(issued), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["Medical Check-up"], result.Value.AppointmentTypeNames);
    }
}
