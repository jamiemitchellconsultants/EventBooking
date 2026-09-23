using EventBooking.Application.Access;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Tests.Attendees;

/// <summary>Verifies locked Attendee Group changes follow every Attendee lifecycle rule.</summary>
public sealed class AttendeeGroupLifecycleTests
{
    private static readonly AttendeeGroup CabinCrew = AttendeeGroup.Define(
        AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
            AppointmentTypeIds.UniformFitting]);
    private static readonly AttendeeGroup GroundTransport = AttendeeGroup.Define(
        AttendeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES",
        "Ground Transport Services", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
            AppointmentTypeIds.UniformFitting]);
    private static readonly AttendeeGroup Pilots = AttendeeGroup.Define(
        AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);

    /// <summary>A set-equivalent active-Booking update preserves all lifecycle state.</summary>
    [Fact]
    public async Task EquivalentGroupPreservesActiveBookingAndUsedInviteHistory()
    {
        var fixture = GivenAttendee(CabinCrew, AttendeeStatus.Booked, activeBooking: true);

        var result = await fixture.Handler.UpdateAsync(
            new UpdateAttendeeCommand(
                fixture.Coordinator, fixture.Attendee.Id, "Amara N.",
                "amara.n@example.com", GroundTransport.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(GroundTransport.Id, fixture.Attendee.AttendeeGroupId);
        Assert.Equal(AttendeeStatus.Booked, fixture.Attendee.Status);
        Assert.Equal(InviteStatus.Used, fixture.Invite!.Status);
        Assert.Equal(
            ["attendee-locked", "initial-invite-locked", "original-booking-locked"],
            fixture.Operations.Events);
    }

    /// <summary>A set-changing active-Booking update fails before any Attendee mutation.</summary>
    [Fact]
    public async Task ChangedGroupConflictsWithActiveOriginalBooking()
    {
        var fixture = GivenAttendee(CabinCrew, AttendeeStatus.Booked, activeBooking: true);

        var result = await fixture.Handler.UpdateAsync(
            new UpdateAttendeeCommand(
                fixture.Coordinator, fixture.Attendee.Id, "Changed", "changed@example.com", Pilots.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("attendee_group_active_booking_conflict", result.Error.Code);
        Assert.Equal(CabinCrew.Id, fixture.Attendee.AttendeeGroupId);
        Assert.Equal("Amara", fixture.Attendee.Name);
    }

    /// <summary>A set-changing pending Invite is superseded without automatic replacement.</summary>
    [Fact]
    public async Task ChangedGroupSupersedesPendingInviteAndResetsStatus()
    {
        var fixture = GivenAttendee(CabinCrew, AttendeeStatus.Invited, activeBooking: false);

        var result = await fixture.Handler.UpdateAsync(
            new UpdateAttendeeCommand(
                fixture.Coordinator, fixture.Attendee.Id, "Amara", "amara@example.com", Pilots.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InviteStatus.Superseded, fixture.Invite!.Status);
        Assert.Equal(AttendeeStatus.NotYetInvited, fixture.Attendee.Status);
        Assert.Equal(Pilots.RequiredAppointmentTypeIds, fixture.Attendee.RequiredAppointmentTypeIds);
        Assert.Equal(1, fixture.Audit.Entries.Count(entry =>
            entry.Action == EventBooking.Domain.Audit.AuditAction.AttendeeGroupReassigned));
    }

    /// <summary>A repeated identical request performs no save and writes no audit row.</summary>
    [Fact]
    public async Task IdenticalUpdateIsANoOp()
    {
        var fixture = GivenAttendee(CabinCrew, AttendeeStatus.NotYetInvited, activeBooking: false,
            includeInvite: false);

        var result = await fixture.Handler.UpdateAsync(
            new UpdateAttendeeCommand(
                fixture.Coordinator, fixture.Attendee.Id, "Amara", "amara@example.com", CabinCrew.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
        Assert.Empty(fixture.Audit.Entries);
    }

    private static Fixture GivenAttendee(
        AttendeeGroup group,
        AttendeeStatus status,
        bool activeBooking,
        bool includeInvite = true)
    {
        var coordinator = Guid.NewGuid();
        var operations = new TransactionOperationLog();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(coordinator, [Role.Coordinator], null));
        var groups = new InMemoryAttendeeGroupRepository();
        groups.Items.AddRange([CabinCrew, GroundTransport, Pilots]);
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        if (status == AttendeeStatus.Invited || status == AttendeeStatus.Booked)
        {
            attendee.MarkInvited();
        }
        if (status == AttendeeStatus.Booked)
        {
            attendee.MarkBooked();
        }
        var attendees = new InMemoryAttendeeRepository(operations);
        attendees.Add(attendee);
        var invites = new InMemoryInviteRepository(operations);
        Invite? invite = null;
        if (includeInvite)
        {
            var eventId = Guid.NewGuid();
            invite = Invite.CreateInitial(
                Guid.NewGuid(), attendee.Id, "token", DateTimeOffset.UtcNow.AddDays(1),
                [eventId, Guid.NewGuid(), Guid.NewGuid()], attendee.RequiredAppointmentTypeIds, 0);
            invites.Add(invite);
        }
        var bookings = new InMemoryBookingRepository(operations);
        if (activeBooking)
        {
            bookings.Add(Booking.Create(
                Guid.NewGuid(), invite!, invite!.OfferedEventIds[0], "manage", DateTimeOffset.UtcNow));
            invite!.MarkUsed();
        }
        var audit = new RecordingAuditLogger();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new SaveAttendeeHandler(
            attendees, groups, invites, bookings, new StaffAccessAuthorizer(profiles), audit, unitOfWork);
        return new Fixture(handler, attendee, invite, coordinator, operations, audit, unitOfWork);
    }

    private sealed record Fixture(
        SaveAttendeeHandler Handler,
        Attendee Attendee,
        Invite? Invite,
        Guid Coordinator,
        TransactionOperationLog Operations,
        RecordingAuditLogger Audit,
        FakeUnitOfWork UnitOfWork);
}
