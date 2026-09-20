# 02a — Deterministic attendee links and the token version counter, edits 15 (Task 9a)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Application.Tests/Attendees/ActiveBookingRequirementTests.cs — 1/1

<!-- retirement-file: {"id":34,"file":"tests/EventBooking.Application.Tests/Attendees/ActiveBookingRequirementTests.cs","beforeSha":"dd64c4955b92e8785f8208a581caaf575bf7ee99a9aa11eabc9c2984820c2280","afterSha":"823047658429423a6c55aec004765f0d78c5415f01da8ef6f0e20995ffdcd00c","side":"after","part":1,"parts":1} -->

`````csharp
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

/// <summary>Verifies attendee requirements cannot drift away from an active booking snapshot.</summary>
public sealed class ActiveBookingRequirementTests
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

    /// <summary>Verifies a changed set conflicts before attendee details or requirements mutate.</summary>
    [Fact]
    public async Task ChangedRequirementsAreRejectedBeforeAnyAttendeeMutation()
    {
        var (handler, attendee, coordinator) = GivenActiveBooking();

        var result = await handler.UpdateAsync(
            new UpdateAttendeeCommand(
                coordinator,
                attendee.Id,
                "Changed Name",
                "changed@example.com",
                Pilots.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("attendee_group_active_booking_conflict", result.Error.Code);
        Assert.Equal(
            "Appointment requirements cannot change while the attendee has an active booking. Cancel and rebook first.",
            result.Error.Message);
        Assert.Equal("Amara Novak", attendee.Name);
        Assert.Equal("amara@example.com", attendee.Email);
        Assert.Equal(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting],
            attendee.RequiredAppointmentTypeIds);
    }

    /// <summary>Verifies a set-equivalent group still permits name and email correction.</summary>
    [Fact]
    public async Task SameRequirementSetInAnotherGroupAllowsDetailCorrection()
    {
        var (handler, attendee, coordinator) = GivenActiveBooking();

        var result = await handler.UpdateAsync(
            new UpdateAttendeeCommand(
                coordinator,
                attendee.Id,
                "Amara N. Novak",
                "amara.novak@example.com",
                GroundTransport.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Amara N. Novak", attendee.Name);
        Assert.Equal("amara.novak@example.com", attendee.Email);
    }

    private static (SaveAttendeeHandler Handler, Attendee Attendee, Guid Coordinator)
        GivenActiveBooking()
    {
        var coordinator = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(coordinator, [Role.Coordinator], null));
        var groups = new InMemoryAttendeeGroupRepository();
        groups.Items.AddRange([CabinCrew, GroundTransport, Pilots]);
        var attendees = new InMemoryAttendeeRepository();
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            CabinCrew,
            ProposalFixture.Now);
        attendees.Add(attendee);
        var bookings = new InMemoryBookingRepository();
        bookings.Add(NewBooking(attendee));

        return (
            new SaveAttendeeHandler(
                attendees,
                groups,
                new InMemoryInviteRepository(),
                bookings,
                new StaffAccessAuthorizer(profiles),
                new RecordingAuditLogger(),
                new FakeClock(),
                new FakeUnitOfWork()),
            attendee,
            coordinator);
    }

    private static Booking NewBooking(Attendee attendee)
    {
        var eventId = Guid.NewGuid();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        return Booking.Create(
            Guid.NewGuid(), invite, eventId, DateTimeOffset.UtcNow);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Attendees/AttendeeGroupLifecycleTests.cs — 1/1

<!-- retirement-file: {"id":35,"file":"tests/EventBooking.Application.Tests/Attendees/AttendeeGroupLifecycleTests.cs","beforeSha":"900d6f09b69252d440319acbc96f07d7470cc4c4406ad7e1ae4495f012c6e234","afterSha":"26092798997b84c6359da68c13a4efff2550e54d00e82835d21a4d6e47458f6c","side":"before","part":1,"parts":1} -->

`````csharp
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
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group, ProposalFixture.Now);
        if (status == AttendeeStatus.Invited || status == AttendeeStatus.Booked)
        {
            attendee.MarkInvited(ProposalFixture.Now);
        }
        if (status == AttendeeStatus.Booked)
        {
            attendee.MarkBooked(ProposalFixture.Now);
        }
        var attendees = new InMemoryAttendeeRepository(operations);
        attendees.Add(attendee);
        var invites = new InMemoryInviteRepository(operations);
        Invite? invite = null;
        if (includeInvite)
        {
            var eventId = Guid.NewGuid();
            invite = Invite.CreateInitial(
                Guid.NewGuid(),
                attendee.Id,
                "token",
                DateTimeOffset.UtcNow.AddDays(1),
                [ProposalFixture.LocationId],
                [eventId, Guid.NewGuid(), Guid.NewGuid()],
                attendee.RequiredAppointmentTypeIds,
                0);
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
            attendees, groups, invites, bookings, new StaffAccessAuthorizer(profiles), audit,
            new FakeClock(), unitOfWork);
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
`````

## after — tests/EventBooking.Application.Tests/Attendees/AttendeeGroupLifecycleTests.cs — 1/1

<!-- retirement-file: {"id":35,"file":"tests/EventBooking.Application.Tests/Attendees/AttendeeGroupLifecycleTests.cs","beforeSha":"900d6f09b69252d440319acbc96f07d7470cc4c4406ad7e1ae4495f012c6e234","afterSha":"26092798997b84c6359da68c13a4efff2550e54d00e82835d21a4d6e47458f6c","side":"after","part":1,"parts":1} -->

`````csharp
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
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group, ProposalFixture.Now);
        if (status == AttendeeStatus.Invited || status == AttendeeStatus.Booked)
        {
            attendee.MarkInvited(ProposalFixture.Now);
        }
        if (status == AttendeeStatus.Booked)
        {
            attendee.MarkBooked(ProposalFixture.Now);
        }
        var attendees = new InMemoryAttendeeRepository(operations);
        attendees.Add(attendee);
        var invites = new InMemoryInviteRepository(operations);
        Invite? invite = null;
        if (includeInvite)
        {
            var eventId = Guid.NewGuid();
            invite = Invite.CreateInitial(
                Guid.NewGuid(),
                attendee.Id,
                DateTimeOffset.UtcNow.AddDays(1),
                [ProposalFixture.LocationId],
                [eventId, Guid.NewGuid(), Guid.NewGuid()],
                attendee.RequiredAppointmentTypeIds,
                0);
            invites.Add(invite);
        }
        var bookings = new InMemoryBookingRepository(operations);
        if (activeBooking)
        {
            bookings.Add(Booking.Create(
                Guid.NewGuid(), invite!, invite!.OfferedEventIds[0], DateTimeOffset.UtcNow));
            invite!.MarkUsed();
        }
        var audit = new RecordingAuditLogger();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new SaveAttendeeHandler(
            attendees, groups, invites, bookings, new StaffAccessAuthorizer(profiles), audit,
            new FakeClock(), unitOfWork);
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
`````

## before — tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs — 1/1

<!-- retirement-file: {"id":36,"file":"tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs","beforeSha":"fc749f2f08c6321f619d478e4ce99f83d4935808f5eefdd52605c009af221f1c","afterSha":"16b1524babfd751e10bd892661f7d30d87ab83d75ba70c516a680942e4fb139c","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Attendees;

public class DeleteAttendeeHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryBookingRepository _bookings = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryBookingAppointmentRepository _appointments;

    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly Attendee _attendee;

    private DeleteAttendeeHandler Handler => new(
        _attendees, _invites, _bookings, _events, _roles,
        new BookingCanceller(_appointments, new InMemoryEventCapacityRepository(_events), _audit),
        _audit,
        _unitOfWork);

    public DeleteAttendeeHandlerTests()
    {
        _appointments = new InMemoryBookingAppointmentRepository(_bookings);
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "a.novak@mail.com",
            AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
            ProposalFixture.Now);
        _attendees.Add(_attendee);
    }

    [Fact]
    public async Task AAttendeeWithNothingOutstandingIsDeletedOutright()
    {
        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_attendees.Items);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnUnconfirmedDeleteOfAAttendeeWithABookingIsRefusedWithAWarning()
    {
        GiveTheAttendeeABooking();

        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            "Deleting this attendee will cancel 1 booking and 1 pending invite, and free the capacity they hold. Confirm to proceed.",
            result.Error.Message);
        Assert.Single(_attendees.Items);
    }

    [Fact]
    public async Task AConfirmedDeleteVoidsTheBookingAndGivesTheCapacityBack()
    {
        var eventItem = GiveTheAttendeeABooking();
        Assert.Equal(9, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_attendees.Items);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(8, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(BookingStatus.Cancelled, _bookings.Items.Single().Status);
        Assert.Equal(InviteStatus.Superseded, _invites.Items.Single().Status);
        Assert.True(_audit.Contains(AuditAction.BookingCancelled));
        Assert.True(_audit.Contains(AuditAction.CapacityIncremented));
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AConfirmedDeleteCascadesToTheActiveRecoveryBookingAndAuditsTheDeletion()
    {
        var eventItem = GiveTheAttendeeABooking();
        var recoveryEvent = GiveTheAttendeeARecoveryBooking();
        Assert.Equal(9, recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_attendees.Items);
        Assert.Equal(2, _bookings.Items.Count);
        Assert.All(_bookings.Items, booking => Assert.Equal(BookingStatus.Cancelled, booking.Status));
        Assert.Equal(10, recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.True(_audit.Contains(AuditAction.AttendeeDeleted));
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AnUnknownAttendeeIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, Guid.NewGuid(), true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task ANonCoordinatorIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Guid.NewGuid(), _attendee.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private Event GiveTheAttendeeARecoveryBooking()
    {
        var original = _bookings.Items.Single();
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 11), new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var recoveryEvent = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(recoveryEvent);

        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            _attendee.Id,
            original.Id,
            "recovery-hash",
            Now.AddDays(4),
            ProposalFixture.LocationId,
            null,
            [recoveryEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(recoveryInvite);

        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoveryEvent.Id, "recovery-manage-hash", Now);
        _bookings.Add(recovery);
        recoveryInvite.MarkUsed();

        recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting));

        return recoveryEvent;
    }

    private Event GiveTheAttendeeABooking()
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(eventItem);

        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            _attendee.Id,
            "hash",
            Now.AddDays(4),
            [ProposalFixture.LocationId],
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            _attendee.RequiredAppointmentTypeIds,
            0);
        _invites.Add(invite);

        var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, "manage-hash", Now);
        _bookings.Add(booking);

        foreach (var typeId in _attendee.RequiredAppointmentTypeIds)
        {
            eventItem.CapacityFor(typeId).Decrement();
            _appointments.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
        }

        return eventItem;
    }
}
`````

## after — tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs — 1/1

<!-- retirement-file: {"id":36,"file":"tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs","beforeSha":"fc749f2f08c6321f619d478e4ce99f83d4935808f5eefdd52605c009af221f1c","afterSha":"16b1524babfd751e10bd892661f7d30d87ab83d75ba70c516a680942e4fb139c","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Attendees;

public class DeleteAttendeeHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryBookingRepository _bookings = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryBookingAppointmentRepository _appointments;

    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly Attendee _attendee;

    private DeleteAttendeeHandler Handler => new(
        _attendees, _invites, _bookings, _events, _roles,
        new BookingCanceller(_appointments, new InMemoryEventCapacityRepository(_events), _audit),
        _audit,
        _unitOfWork);

    public DeleteAttendeeHandlerTests()
    {
        _appointments = new InMemoryBookingAppointmentRepository(_bookings);
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "a.novak@mail.com",
            AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
            ProposalFixture.Now);
        _attendees.Add(_attendee);
    }

    [Fact]
    public async Task AAttendeeWithNothingOutstandingIsDeletedOutright()
    {
        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_attendees.Items);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnUnconfirmedDeleteOfAAttendeeWithABookingIsRefusedWithAWarning()
    {
        GiveTheAttendeeABooking();

        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            "Deleting this attendee will cancel 1 booking and 1 pending invite, and free the capacity they hold. Confirm to proceed.",
            result.Error.Message);
        Assert.Single(_attendees.Items);
    }

    [Fact]
    public async Task AConfirmedDeleteVoidsTheBookingAndGivesTheCapacityBack()
    {
        var eventItem = GiveTheAttendeeABooking();
        Assert.Equal(9, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_attendees.Items);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(8, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(BookingStatus.Cancelled, _bookings.Items.Single().Status);
        Assert.Equal(InviteStatus.Superseded, _invites.Items.Single().Status);
        Assert.True(_audit.Contains(AuditAction.BookingCancelled));
        Assert.True(_audit.Contains(AuditAction.CapacityIncremented));
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AConfirmedDeleteCascadesToTheActiveRecoveryBookingAndAuditsTheDeletion()
    {
        var eventItem = GiveTheAttendeeABooking();
        var recoveryEvent = GiveTheAttendeeARecoveryBooking();
        Assert.Equal(9, recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_attendees.Items);
        Assert.Equal(2, _bookings.Items.Count);
        Assert.All(_bookings.Items, booking => Assert.Equal(BookingStatus.Cancelled, booking.Status));
        Assert.Equal(10, recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.True(_audit.Contains(AuditAction.AttendeeDeleted));
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AnUnknownAttendeeIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, Guid.NewGuid(), true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task ANonCoordinatorIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Guid.NewGuid(), _attendee.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private Event GiveTheAttendeeARecoveryBooking()
    {
        var original = _bookings.Items.Single();
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 11), new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var recoveryEvent = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(recoveryEvent);

        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            _attendee.Id,
            original.Id,
            Now.AddDays(4),
            ProposalFixture.LocationId,
            null,
            [recoveryEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(recoveryInvite);

        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoveryEvent.Id, Now);
        _bookings.Add(recovery);
        recoveryInvite.MarkUsed();

        recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting));

        return recoveryEvent;
    }

    private Event GiveTheAttendeeABooking()
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(eventItem);

        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            _attendee.Id,
            Now.AddDays(4),
            [ProposalFixture.LocationId],
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            _attendee.RequiredAppointmentTypeIds,
            0);
        _invites.Add(invite);

        var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, Now);
        _bookings.Add(booking);

        foreach (var typeId in _attendee.RequiredAppointmentTypeIds)
        {
            eventItem.CapacityFor(typeId).Decrement();
            _appointments.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
        }

        return eventItem;
    }
}
`````

## before — tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs — 1/1

<!-- retirement-file: {"id":37,"file":"tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs","beforeSha":"ffa5042cc0da58fe05860489807010a362783612f0329f472f0d69b009c701a7","afterSha":"2406c06aea39a74c0807346e27510ded7b95dafe0fe45187fc7e8ca01cb52d8f","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies booking confirmation snapshots required operational appointments atomically.</summary>
public sealed class BookingAppointmentSnapshotTests
{
    /// <summary>Verifies one Expected appointment is created for each current attendee requirement.</summary>
    [Fact]
    public async Task ConfirmationCreatesOneAppointmentPerRequirementBeforeTheSingleSave()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero));
        var tokens = new FakeTokenService();
        var attendees = new InMemoryAttendeeRepository();
        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            pilots,
            ProposalFixture.Now);
        attendees.Add(attendee);
        attendee.MarkInvited(ProposalFixture.Now);

        var events = new InMemoryEventRepository();
        var selected = AddEvent(events, new DateOnly(2026, 9, 8));
        var second = AddEvent(events, new DateOnly(2026, 9, 9));
        var third = AddEvent(events, new DateOnly(2026, 9, 10));
        var inviteId = Guid.NewGuid();
        var token = tokens.Issue(inviteId);
        var invites = new InMemoryInviteRepository();
        invites.Add(Invite.CreateInitial(
            inviteId,
            attendee.Id,
            token.TokenHash,
            clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            [selected.Id, second.Id, third.Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            0));

        var bookings = new InMemoryBookingRepository();
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        var unitOfWork = new FakeUnitOfWork();
        var deliveries = EmailDeliveryTestFactory.Create(
            new InMemoryEmailDeliveryRepository(),
            new RecordingEmailSender(),
            new FakeUnitOfWork(),
            clock);
        var handler = new ConfirmBookingHandler(
            invites,
            attendees,
            events,
            bookings,
            appointments,
            new InMemoryEventCapacityRepository(events),
            new EligibleEventFinder(events, clock),
            tokens,
            deliveries,
            new RecordingAuditLogger(),
            unitOfWork,
            clock,
            new AttendeePortalOptions(
                "https://booking.example.com", "help@example.com"));

        var result = await handler.HandleAsync(
            new ConfirmBookingCommand(token.Token, selected.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, appointments.Items.Count);
        Assert.All(appointments.Items, value =>
        {
            Assert.Equal(result.Value.BookingId, value.BookingId);
            Assert.Equal(BookingAppointmentStatus.Expected, value.Status);
            Assert.Equal(1, value.Version);
        });
        Assert.Equal(
            attendee.RequiredAppointmentTypeIds.OrderBy(value => value),
            appointments.Items.Select(value => value.AppointmentTypeId).OrderBy(value => value));
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(1, unitOfWork.CommitCount);
    }

    /// <summary>Verifies one-, two-, and three-type snapshots each produce their exact set.</summary>
    [Theory]
    [InlineData("MED")]
    [InlineData("DAT,UNI")]
    [InlineData("DAT,MED,UNI")]
    public async Task ConfirmationCreatesTheExactSnapshotSet(string codes)
    {
        var byCode = new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            ["DAT"] = AppointmentTypeIds.DrugAndAlcoholTesting,
            ["MED"] = AppointmentTypeIds.MedicalCheckUp,
            ["UNI"] = AppointmentTypeIds.UniformFitting,
        };
        var snapshot = codes.Split(',').Select(code => byCode[code]).ToList();

        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero));
        var tokens = new FakeTokenService();
        var attendees = new InMemoryAttendeeRepository();
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            AttendeeGroup.Define(
                Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting]),
            ProposalFixture.Now);
        attendees.Add(attendee);
        attendee.MarkInvited(ProposalFixture.Now);

        var events = new InMemoryEventRepository();
        var selected = AddEvent(events, new DateOnly(2026, 9, 8));
        var inviteId = Guid.NewGuid();
        var token = tokens.Issue(inviteId);
        var invites = new InMemoryInviteRepository();
        invites.Add(Invite.CreateInitial(
            inviteId,
            attendee.Id,
            token.TokenHash,
            clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            [selected.Id, AddEvent(events, new DateOnly(2026, 9, 9)).Id,
                AddEvent(events, new DateOnly(2026, 9, 10)).Id],
            snapshot,
            0));

        var bookings = new InMemoryBookingRepository();
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ConfirmBookingHandler(
            invites,
            attendees,
            events,
            bookings,
            appointments,
            new InMemoryEventCapacityRepository(events),
            new EligibleEventFinder(events, clock),
            tokens,
            EmailDeliveryTestFactory.Create(
                new InMemoryEmailDeliveryRepository(),
                new RecordingEmailSender(),
                new FakeUnitOfWork(),
                clock),
            new RecordingAuditLogger(),
            unitOfWork,
            clock,
            new AttendeePortalOptions(
                "https://booking.example.com", "help@example.com"));

        // Each snapshot size needs its matching group so confirmation proceeds.
        var group = snapshot.Count switch
        {
            1 => AttendeeGroup.Define(
                AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            2 => AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
            _ => AttendeeGroup.Define(
                AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                    AppointmentTypeIds.UniformFitting]),
        };
        attendee.AssignAttendeeGroup(group);

        var result = await handler.HandleAsync(
            new ConfirmBookingCommand(token.Token, selected.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            snapshot.OrderBy(value => value),
            appointments.Items.Select(value => value.AppointmentTypeId).OrderBy(value => value));
    }

    private static Event AddEvent(
        InMemoryEventRepository events,
        DateOnly date)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        events.Add(eventItem);
        return eventItem;
    }
}
`````

## after — tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs — 1/1

<!-- retirement-file: {"id":37,"file":"tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs","beforeSha":"ffa5042cc0da58fe05860489807010a362783612f0329f472f0d69b009c701a7","afterSha":"2406c06aea39a74c0807346e27510ded7b95dafe0fe45187fc7e8ca01cb52d8f","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies booking confirmation snapshots required operational appointments atomically.</summary>
public sealed class BookingAppointmentSnapshotTests
{
    /// <summary>Verifies one Expected appointment is created for each current attendee requirement.</summary>
    [Fact]
    public async Task ConfirmationCreatesOneAppointmentPerRequirementBeforeTheSingleSave()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero));
        var tokens = new FakeTokenService();
        var attendees = new InMemoryAttendeeRepository();
        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            pilots,
            ProposalFixture.Now);
        attendees.Add(attendee);
        attendee.MarkInvited(ProposalFixture.Now);

        var events = new InMemoryEventRepository();
        var selected = AddEvent(events, new DateOnly(2026, 9, 8));
        var second = AddEvent(events, new DateOnly(2026, 9, 9));
        var third = AddEvent(events, new DateOnly(2026, 9, 10));
        var inviteId = Guid.NewGuid();
        var token = tokens.Issue(TokenPurpose.Book, inviteId, Invite.InitialTokenVersion);
        var invites = new InMemoryInviteRepository();
        invites.Add(Invite.CreateInitial(
            inviteId,
            attendee.Id,
            clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            [selected.Id, second.Id, third.Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            0));

        var bookings = new InMemoryBookingRepository();
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        var unitOfWork = new FakeUnitOfWork();
        var deliveries = EmailDeliveryTestFactory.Create(
            new InMemoryEmailDeliveryRepository(),
            new RecordingEmailSender(),
            new FakeUnitOfWork(),
            clock);
        var handler = new ConfirmBookingHandler(
            invites,
            attendees,
            events,
            bookings,
            appointments,
            new InMemoryEventCapacityRepository(events),
            new EligibleEventFinder(events, clock),
            tokens,
            deliveries,
            new RecordingAuditLogger(),
            unitOfWork,
            clock,
            new AttendeePortalOptions(
                "https://booking.example.com", "help@example.com"));

        var result = await handler.HandleAsync(
            new ConfirmBookingCommand(token, selected.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, appointments.Items.Count);
        Assert.All(appointments.Items, value =>
        {
            Assert.Equal(result.Value.BookingId, value.BookingId);
            Assert.Equal(BookingAppointmentStatus.Expected, value.Status);
            Assert.Equal(1, value.Version);
        });
        Assert.Equal(
            attendee.RequiredAppointmentTypeIds.OrderBy(value => value),
            appointments.Items.Select(value => value.AppointmentTypeId).OrderBy(value => value));
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(1, unitOfWork.CommitCount);
    }

    /// <summary>Verifies one-, two-, and three-type snapshots each produce their exact set.</summary>
    [Theory]
    [InlineData("MED")]
    [InlineData("DAT,UNI")]
    [InlineData("DAT,MED,UNI")]
    public async Task ConfirmationCreatesTheExactSnapshotSet(string codes)
    {
        var byCode = new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            ["DAT"] = AppointmentTypeIds.DrugAndAlcoholTesting,
            ["MED"] = AppointmentTypeIds.MedicalCheckUp,
            ["UNI"] = AppointmentTypeIds.UniformFitting,
        };
        var snapshot = codes.Split(',').Select(code => byCode[code]).ToList();

        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero));
        var tokens = new FakeTokenService();
        var attendees = new InMemoryAttendeeRepository();
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            AttendeeGroup.Define(
                Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting]),
            ProposalFixture.Now);
        attendees.Add(attendee);
        attendee.MarkInvited(ProposalFixture.Now);

        var events = new InMemoryEventRepository();
        var selected = AddEvent(events, new DateOnly(2026, 9, 8));
        var inviteId = Guid.NewGuid();
        var token = tokens.Issue(TokenPurpose.Book, inviteId, Invite.InitialTokenVersion);
        var invites = new InMemoryInviteRepository();
        invites.Add(Invite.CreateInitial(
            inviteId,
            attendee.Id,
            clock.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            [selected.Id, AddEvent(events, new DateOnly(2026, 9, 9)).Id,
                AddEvent(events, new DateOnly(2026, 9, 10)).Id],
            snapshot,
            0));

        var bookings = new InMemoryBookingRepository();
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ConfirmBookingHandler(
            invites,
            attendees,
            events,
            bookings,
            appointments,
            new InMemoryEventCapacityRepository(events),
            new EligibleEventFinder(events, clock),
            tokens,
            EmailDeliveryTestFactory.Create(
                new InMemoryEmailDeliveryRepository(),
                new RecordingEmailSender(),
                new FakeUnitOfWork(),
                clock),
            new RecordingAuditLogger(),
            unitOfWork,
            clock,
            new AttendeePortalOptions(
                "https://booking.example.com", "help@example.com"));

        // Each snapshot size needs its matching group so confirmation proceeds.
        var group = snapshot.Count switch
        {
            1 => AttendeeGroup.Define(
                AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            2 => AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
            _ => AttendeeGroup.Define(
                AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                    AppointmentTypeIds.UniformFitting]),
        };
        attendee.AssignAttendeeGroup(group);

        var result = await handler.HandleAsync(
            new ConfirmBookingCommand(token, selected.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            snapshot.OrderBy(value => value),
            appointments.Items.Select(value => value.AppointmentTypeId).OrderBy(value => value));
    }

    private static Event AddEvent(
        InMemoryEventRepository events,
        DateOnly date)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        events.Add(eventItem);
        return eventItem;
    }
}
`````
