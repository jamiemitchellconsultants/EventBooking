# 00b — Vocabulary edits 90 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs — 1/1

<!-- vocabulary-file: {"id":304,"oldPath":"tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs","newPath":"tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs","beforeSha":"683d0a4a461174b92c221e35af1a451b93a68887e236a97da1527ea8e1807c7e","afterSha":"676c1d2df6d57896fefeda6026b3526b874a5c245cd333de0c04e62cc1eccb48","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Bookings;

public class BookingTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid EventA = Guid.Parse("50000001-0000-0000-0000-000000000001");
    private static readonly Guid EventB = Guid.Parse("50000002-0000-0000-0000-000000000002");
    private static readonly Guid EventC = Guid.Parse("50000003-0000-0000-0000-000000000003");
    private static readonly Guid EventNotOffered = Guid.Parse("50000009-0000-0000-0000-000000000009");

    private static Invite NewInvite() =>
        Invite.CreateInitial(
            Guid.NewGuid(), Guid.NewGuid(), "invite-token-hash", Now.AddDays(4),
            [EventA, EventB, EventC], [AppointmentTypeIds.DrugAndAlcoholTesting], 0);

    private static Booking NewBooking(Invite invite) =>
        Booking.Create(Guid.NewGuid(), invite, EventB, "manage-token-hash", Now);

    [Fact]
    public void ABookingCarriesTheAttendeeEventAndInvite()
    {
        var invite = NewInvite();

        var booking = NewBooking(invite);

        Assert.Equal(invite.AttendeeId, booking.AttendeeId);
        Assert.Equal(EventB, booking.EventId);
        Assert.Equal(invite.Id, booking.InviteId);
        Assert.Equal(Now, booking.CreatedAt);
        Assert.Equal(BookingStatus.Active, booking.Status);
        Assert.Equal("manage-token-hash", booking.ManageTokenHash);
    }

    [Fact]
    public void BookingAEventTheInviteNeverOfferedIsRejected()
    {
        var invite = NewInvite();

        var ex = Assert.Throws<DomainException>(
            () => Booking.Create(Guid.NewGuid(), invite, EventNotOffered, "manage-token-hash", Now));
        Assert.Equal("The chosen eventItem is not one of this invite's options.", ex.Message);
    }

    [Fact]
    public void BookingAnInviteThatIsNoLongerPendingIsRejected()
    {
        var invite = NewInvite();
        invite.MarkExpired();

        var ex = Assert.Throws<DomainException>(() => NewBooking(invite));
        Assert.Equal("This invite can no longer be used.", ex.Message);
    }

    [Fact]
    public void ABookingWithoutAManageTokenHashIsRejected()
    {
        var invite = NewInvite();

        var ex = Assert.Throws<DomainException>(
            () => Booking.Create(Guid.NewGuid(), invite, EventB, " ", Now));
        Assert.Equal("manageTokenHash must not be blank.", ex.Message);
    }

    [Fact]
    public void CreatingABookingDoesNotConsumeTheInvite()
    {
        var invite = NewInvite();

        NewBooking(invite);

        Assert.Equal(InviteStatus.Pending, invite.Status);
    }

    [Fact]
    public void CancellingMarksTheBookingCancelled()
    {
        var booking = NewBooking(NewInvite());

        booking.Cancel();

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
    }

    [Fact]
    public void CancellingTwiceIsRejected()
    {
        var booking = NewBooking(NewInvite());
        booking.Cancel();

        var ex = Assert.Throws<DomainException>(() => booking.Cancel());
        Assert.Equal("This booking has already been cancelled.", ex.Message);
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs — 1/1

<!-- vocabulary-file: {"id":305,"oldPath":"tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs","newPath":"tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs","beforeSha":"cc440e7867220064ebd81c68fe48ab91e127602b140526ce26875d5301b413d5","afterSha":"baf99bb238b154c6c98372c80e77e84dd001924e45ba9c0ecddc72924e84004f","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Bookings;

/// <summary>Verifies every recovery points directly to an immutable active journey root.</summary>
public sealed class RecoveryBookingTests
{
    /// <summary>A recovery Booking copies the original root and can conclude and reopen.</summary>
    [Fact]
    public void RecoveryLifecyclePreservesOriginalRoot()
    {
        var candidateId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var initialInvite = Invite.CreateInitial(
            Guid.NewGuid(), candidateId, "initial", DateTimeOffset.UtcNow.AddDays(1),
            [slotId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        var original = Booking.Create(
            Guid.NewGuid(), initialInvite, slotId, "manage-original", DateTimeOffset.UtcNow);
        var recoverySlot = Guid.NewGuid();
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), candidateId, original.Id, "recovery", DateTimeOffset.UtcNow.AddDays(2),
            [recoverySlot, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);

        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoverySlot,
            "manage-recovery", DateTimeOffset.UtcNow.AddHours(1));
        recovery.Conclude();
        recovery.Reopen();

        Assert.Null(original.RecoveryOfBookingId);
        Assert.Equal(BookingStatus.Active, original.Status);
        Assert.Equal(original.Id, recovery.RecoveryOfBookingId);
        Assert.Equal(BookingStatus.Active, recovery.Status);
    }

    /// <summary>A recovery cannot point at another recovery Booking.</summary>
    [Fact]
    public void RecoveryChainIsRejected()
    {
        var candidateId = Guid.NewGuid();
        var rootSlot = Guid.NewGuid();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(), candidateId, "initial", DateTimeOffset.UtcNow.AddDays(1),
            [rootSlot, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        var root = Booking.Create(Guid.NewGuid(), initial, rootSlot, "root", DateTimeOffset.UtcNow);
        var firstSlot = Guid.NewGuid();
        var firstInvite = Invite.CreateRecovery(
            Guid.NewGuid(), candidateId, root.Id, "first", DateTimeOffset.UtcNow.AddDays(1),
            [firstSlot, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
        var first = Booking.CreateRecovery(
            Guid.NewGuid(), firstInvite, root, firstSlot, "first-manage", DateTimeOffset.UtcNow);
        var secondSlot = Guid.NewGuid();
        var secondInvite = Invite.CreateRecovery(
            Guid.NewGuid(), candidateId, root.Id, "second", DateTimeOffset.UtcNow.AddDays(1),
            [secondSlot, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);

        Assert.Throws<EventBooking.Domain.Common.DomainException>(() => Booking.CreateRecovery(
            Guid.NewGuid(), secondInvite, first, secondSlot, "second-manage", DateTimeOffset.UtcNow));
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs — 1/1

<!-- vocabulary-file: {"id":305,"oldPath":"tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs","newPath":"tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs","beforeSha":"cc440e7867220064ebd81c68fe48ab91e127602b140526ce26875d5301b413d5","afterSha":"baf99bb238b154c6c98372c80e77e84dd001924e45ba9c0ecddc72924e84004f","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Bookings;

/// <summary>Verifies every recovery points directly to an immutable active journey root.</summary>
public sealed class RecoveryBookingTests
{
    /// <summary>A recovery Booking copies the original root and can conclude and reopen.</summary>
    [Fact]
    public void RecoveryLifecyclePreservesOriginalRoot()
    {
        var attendeeId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var initialInvite = Invite.CreateInitial(
            Guid.NewGuid(), attendeeId, "initial", DateTimeOffset.UtcNow.AddDays(1),
            [eventId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        var original = Booking.Create(
            Guid.NewGuid(), initialInvite, eventId, "manage-original", DateTimeOffset.UtcNow);
        var recoveryEvent = Guid.NewGuid();
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), attendeeId, original.Id, "recovery", DateTimeOffset.UtcNow.AddDays(2),
            [recoveryEvent, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);

        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoveryEvent,
            "manage-recovery", DateTimeOffset.UtcNow.AddHours(1));
        recovery.Conclude();
        recovery.Reopen();

        Assert.Null(original.RecoveryOfBookingId);
        Assert.Equal(BookingStatus.Active, original.Status);
        Assert.Equal(original.Id, recovery.RecoveryOfBookingId);
        Assert.Equal(BookingStatus.Active, recovery.Status);
    }

    /// <summary>A recovery cannot point at another recovery Booking.</summary>
    [Fact]
    public void RecoveryChainIsRejected()
    {
        var attendeeId = Guid.NewGuid();
        var rootEvent = Guid.NewGuid();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(), attendeeId, "initial", DateTimeOffset.UtcNow.AddDays(1),
            [rootEvent, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        var root = Booking.Create(Guid.NewGuid(), initial, rootEvent, "root", DateTimeOffset.UtcNow);
        var firstEvent = Guid.NewGuid();
        var firstInvite = Invite.CreateRecovery(
            Guid.NewGuid(), attendeeId, root.Id, "first", DateTimeOffset.UtcNow.AddDays(1),
            [firstEvent, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
        var first = Booking.CreateRecovery(
            Guid.NewGuid(), firstInvite, root, firstEvent, "first-manage", DateTimeOffset.UtcNow);
        var secondEvent = Guid.NewGuid();
        var secondInvite = Invite.CreateRecovery(
            Guid.NewGuid(), attendeeId, root.Id, "second", DateTimeOffset.UtcNow.AddDays(1),
            [secondEvent, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);

        Assert.Throws<EventBooking.Domain.Common.DomainException>(() => Booking.CreateRecovery(
            Guid.NewGuid(), secondInvite, first, secondEvent, "second-manage", DateTimeOffset.UtcNow));
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Candidates/CandidateStatusTests.cs — 1/1

<!-- vocabulary-file: {"id":306,"oldPath":"tests/EventBooking.Domain.Tests/Candidates/CandidateStatusTests.cs","newPath":"tests/EventBooking.Domain.Tests/Attendees/AttendeeStatusTests.cs","beforeSha":"897db9bf0c6836ad49062f35cd1477d05e284b882761c53f28c3a41e559cb42b","afterSha":"11f1c7504ce1cc37dc5b7f3e2b1002b5ddde01020ec4bfd61b8bd0fb1207e319","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Domain.Tests.Candidates;

public class CandidateStatusTests
{
    /// <summary>Builds a DAT-only group; lifecycle tests need a mapping, not an identity.</summary>
    private static EmployeeGroup DatOnly() =>
        EmployeeGroup.Define(
            Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);

    private static Candidate NewCandidate() =>
        Candidate.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", DatOnly());

    private static Candidate InvitedCandidate()
    {
        var candidate = NewCandidate();
        candidate.MarkInvited();
        return candidate;
    }

    [Fact]
    public void ANotYetInvitedCandidateCanBeInvited()
    {
        var candidate = NewCandidate();

        candidate.MarkInvited();

        Assert.Equal(CandidateStatus.Invited, candidate.Status);
    }

    [Fact]
    public void ACandidateWithNoEligibleSlotsBecomesAwaitingAvailability()
    {
        var candidate = NewCandidate();

        candidate.MarkAwaitingAvailability();

        Assert.Equal(CandidateStatus.AwaitingAvailability, candidate.Status);
    }

    [Fact]
    public void AnAwaitingCandidateCanBeInvitedOnceSlotsAppear()
    {
        var candidate = NewCandidate();
        candidate.MarkAwaitingAvailability();

        candidate.MarkInvited();

        Assert.Equal(CandidateStatus.Invited, candidate.Status);
    }

    [Fact]
    public void ReInvitingAnAlreadyInvitedCandidateIsAllowed()
    {
        var candidate = InvitedCandidate();

        candidate.MarkInvited();

        Assert.Equal(CandidateStatus.Invited, candidate.Status);
    }

    [Fact]
    public void OnlyAnInvitedCandidateCanBecomeBooked()
    {
        var candidate = InvitedCandidate();
        candidate.MarkBooked();
        Assert.Equal(CandidateStatus.Booked, candidate.Status);

        var notInvited = NewCandidate();
        var ex = Assert.Throws<DomainException>(() => notInvited.MarkBooked());
        Assert.Equal("A candidate cannot move from NotYetInvited to Booked.", ex.Message);
    }

    [Fact]
    public void OnlyAnInvitedCandidateCanRunOutOfRetries()
    {
        var candidate = InvitedCandidate();
        candidate.MarkNoResponse();
        Assert.Equal(CandidateStatus.NoResponseNeedsFollowUp, candidate.Status);

        var booked = InvitedCandidate();
        booked.MarkBooked();
        Assert.Throws<DomainException>(() => booked.MarkNoResponse());
    }

    [Fact]
    public void AFollowUpCandidateCanBeManuallyReInvited()
    {
        var candidate = InvitedCandidate();
        candidate.MarkNoResponse();

        candidate.MarkInvited();

        Assert.Equal(CandidateStatus.Invited, candidate.Status);
    }

    [Fact]
    public void CancellingABookingReturnsTheCandidateToNotYetInvited()
    {
        var candidate = InvitedCandidate();
        candidate.MarkBooked();

        candidate.ResetToNotYetInvited();

        Assert.Equal(CandidateStatus.NotYetInvited, candidate.Status);
    }

    [Fact]
    public void ANotYetInvitedCandidateCannotBeResetAgain()
    {
        var candidate = NewCandidate();

        var ex = Assert.Throws<DomainException>(() => candidate.ResetToNotYetInvited());
        Assert.Equal("A candidate cannot move from NotYetInvited to NotYetInvited.", ex.Message);
    }

}
`````

## after — tests/EventBooking.Domain.Tests/Attendees/AttendeeStatusTests.cs — 1/1

<!-- vocabulary-file: {"id":306,"oldPath":"tests/EventBooking.Domain.Tests/Candidates/CandidateStatusTests.cs","newPath":"tests/EventBooking.Domain.Tests/Attendees/AttendeeStatusTests.cs","beforeSha":"897db9bf0c6836ad49062f35cd1477d05e284b882761c53f28c3a41e559cb42b","afterSha":"11f1c7504ce1cc37dc5b7f3e2b1002b5ddde01020ec4bfd61b8bd0fb1207e319","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Domain.Tests.Attendees;

public class AttendeeStatusTests
{
    /// <summary>Builds a DAT-only group; lifecycle tests need a mapping, not an identity.</summary>
    private static AttendeeGroup DatOnly() =>
        AttendeeGroup.Define(
            Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);

    private static Attendee NewAttendee() =>
        Attendee.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", DatOnly());

    private static Attendee InvitedAttendee()
    {
        var attendee = NewAttendee();
        attendee.MarkInvited();
        return attendee;
    }

    [Fact]
    public void ANotYetInvitedAttendeeCanBeInvited()
    {
        var attendee = NewAttendee();

        attendee.MarkInvited();

        Assert.Equal(AttendeeStatus.Invited, attendee.Status);
    }

    [Fact]
    public void AAttendeeWithNoEligibleEventsBecomesAwaitingAvailability()
    {
        var attendee = NewAttendee();

        attendee.MarkAwaitingAvailability();

        Assert.Equal(AttendeeStatus.AwaitingAvailability, attendee.Status);
    }

    [Fact]
    public void AnAwaitingAttendeeCanBeInvitedOnceEventsAppear()
    {
        var attendee = NewAttendee();
        attendee.MarkAwaitingAvailability();

        attendee.MarkInvited();

        Assert.Equal(AttendeeStatus.Invited, attendee.Status);
    }

    [Fact]
    public void ReInvitingAnAlreadyInvitedAttendeeIsAllowed()
    {
        var attendee = InvitedAttendee();

        attendee.MarkInvited();

        Assert.Equal(AttendeeStatus.Invited, attendee.Status);
    }

    [Fact]
    public void OnlyAnInvitedAttendeeCanBecomeBooked()
    {
        var attendee = InvitedAttendee();
        attendee.MarkBooked();
        Assert.Equal(AttendeeStatus.Booked, attendee.Status);

        var notInvited = NewAttendee();
        var ex = Assert.Throws<DomainException>(() => notInvited.MarkBooked());
        Assert.Equal("A attendee cannot move from NotYetInvited to Booked.", ex.Message);
    }

    [Fact]
    public void OnlyAnInvitedAttendeeCanRunOutOfRetries()
    {
        var attendee = InvitedAttendee();
        attendee.MarkNoResponse();
        Assert.Equal(AttendeeStatus.NoResponseNeedsFollowUp, attendee.Status);

        var booked = InvitedAttendee();
        booked.MarkBooked();
        Assert.Throws<DomainException>(() => booked.MarkNoResponse());
    }

    [Fact]
    public void AFollowUpAttendeeCanBeManuallyReInvited()
    {
        var attendee = InvitedAttendee();
        attendee.MarkNoResponse();

        attendee.MarkInvited();

        Assert.Equal(AttendeeStatus.Invited, attendee.Status);
    }

    [Fact]
    public void CancellingABookingReturnsTheAttendeeToNotYetInvited()
    {
        var attendee = InvitedAttendee();
        attendee.MarkBooked();

        attendee.ResetToNotYetInvited();

        Assert.Equal(AttendeeStatus.NotYetInvited, attendee.Status);
    }

    [Fact]
    public void ANotYetInvitedAttendeeCannotBeResetAgain()
    {
        var attendee = NewAttendee();

        var ex = Assert.Throws<DomainException>(() => attendee.ResetToNotYetInvited());
        Assert.Equal("A attendee cannot move from NotYetInvited to NotYetInvited.", ex.Message);
    }

}
`````

## before — tests/EventBooking.Domain.Tests/Candidates/CandidateTests.cs — 1/1

<!-- vocabulary-file: {"id":307,"oldPath":"tests/EventBooking.Domain.Tests/Candidates/CandidateTests.cs","newPath":"tests/EventBooking.Domain.Tests/Attendees/AttendeeTests.cs","beforeSha":"760d89bb92d13ea0b5a3ad0d606364f597cc68f9dc62741b0136e0d41070dcbb","afterSha":"96b980c36a7d4539ea53aaa44fa9c650bfb0c815697dd9521fede394d0dcab98","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Domain.Tests.Candidates;

public class CandidateTests
{
    private static readonly Guid[] TwoTypes =
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting];

    /// <summary>Builds the Pilots group used across these fixtures for DAT+UNI.</summary>
    private static EmployeeGroup Pilots() =>
        EmployeeGroup.Define(EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true, TwoTypes);

    private static Candidate NewCandidate() =>
        Candidate.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", Pilots());

    [Fact]
    public void ANewCandidateStartsNotYetInvited()
    {
        var candidate = NewCandidate();

        Assert.Equal(CandidateStatus.NotYetInvited, candidate.Status);
        Assert.Equal("Amara Novak", candidate.Name);
        Assert.Equal("a.novak@mail.com", candidate.Email);
    }

    [Fact]
    public void NameAndEmailAreTrimmedAndTheEmailIsLowerCased()
    {
        var candidate = Candidate.Create(
            Guid.NewGuid(), "  Amara Novak  ", "  A.Novak@Mail.COM ", Pilots());

        Assert.Equal("Amara Novak", candidate.Name);
        Assert.Equal("a.novak@mail.com", candidate.Email);
    }

    [Fact]
    public void RequirementsAreRecordedAgainstTheCandidate()
    {
        var candidate = NewCandidate();

        Assert.Equal(2, candidate.Requirements.Count);
        Assert.All(candidate.Requirements, r => Assert.Equal(candidate.Id, r.CandidateId));
        Assert.Equal(
            TwoTypes.OrderBy(id => id),
            candidate.RequiredAppointmentTypeIds.OrderBy(id => id));
    }

    [Fact]
    public void AMissingNameIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => Candidate.Create(Guid.NewGuid(), "  ", "a.novak@mail.com", Pilots()));
        Assert.Equal("name must not be blank.", ex.Message);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("no@domain")]
    [InlineData("two@@at.com")]
    [InlineData("spaces in@mail.com")]
    [InlineData("")]
    [InlineData(null)]
    public void AnInvalidEmailIsRejected(string? email)
    {
        var ex = Assert.Throws<DomainException>(
            () => Candidate.Create(Guid.NewGuid(), "Amara Novak", email, Pilots()));
        Assert.Equal("email is not a valid email address.", ex.Message);
    }

    [Fact]
    public void AGroupWithNoMappedTypesIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => EmployeeGroup.Define(Guid.NewGuid(), "EMPTY", "Empty", true, []));
        Assert.Equal("An employee group must map at least one appointment type.", ex.Message);
    }

    [Fact]
    public void DuplicateMappedTypesAreRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => EmployeeGroup.Define(
                Guid.NewGuid(),
                "DUP",
                "Dup",
                true,
                [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.MedicalCheckUp]));
        Assert.Equal("An employee group cannot map the same appointment type twice.", ex.Message);
    }

    [Fact]
    public void AnUnknownMappedTypeIsRejected()
    {
        Assert.Throws<DomainException>(
            () => EmployeeGroup.Define(Guid.NewGuid(), "UNKNOWN", "Unknown", true, [Guid.NewGuid()]));
    }

    [Fact]
    public void AllThreeAppointmentTypesAreAllowed()
    {
        var cabinCrew = EmployeeGroup.Define(
            EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true, AppointmentTypeIds.All);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", cabinCrew);

        Assert.Equal(3, candidate.Requirements.Count);
    }

    [Fact]
    public void UpdatingDetailsRevalidates()
    {
        var candidate = NewCandidate();

        candidate.UpdateDetails("Amara N. Novak", "amara@mail.com");
        Assert.Equal("Amara N. Novak", candidate.Name);
        Assert.Equal("amara@mail.com", candidate.Email);

        Assert.Throws<DomainException>(() => candidate.UpdateDetails("Amara", "broken"));
        Assert.Equal("amara@mail.com", candidate.Email);
    }

    [Fact]
    public void AssigningADifferentGroupReplacesThePreviousSet()
    {
        var candidate = NewCandidate();
        var groundOps = EmployeeGroup.Define(
            EmployeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT",
            "Ground Operations Agent", true, [AppointmentTypeIds.MedicalCheckUp]);

        Assert.True(candidate.AssignEmployeeGroup(groundOps));
        Assert.Equal([AppointmentTypeIds.MedicalCheckUp], candidate.RequiredAppointmentTypeIds);
    }

    [Fact]
    public void AssigningAnEquivalentGroupPreservesThePreviousSet()
    {
        var candidate = NewCandidate();
        var equivalent = EmployeeGroup.Define(
            Guid.NewGuid(), "PILOTS_EQUIVALENT", "Pilots equivalent", true, TwoTypes);

        Assert.False(candidate.AssignEmployeeGroup(equivalent));
        Assert.Equal(2, candidate.Requirements.Count);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Attendees/AttendeeTests.cs — 1/1

<!-- vocabulary-file: {"id":307,"oldPath":"tests/EventBooking.Domain.Tests/Candidates/CandidateTests.cs","newPath":"tests/EventBooking.Domain.Tests/Attendees/AttendeeTests.cs","beforeSha":"760d89bb92d13ea0b5a3ad0d606364f597cc68f9dc62741b0136e0d41070dcbb","afterSha":"96b980c36a7d4539ea53aaa44fa9c650bfb0c815697dd9521fede394d0dcab98","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Domain.Tests.Attendees;

public class AttendeeTests
{
    private static readonly Guid[] TwoTypes =
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting];

    /// <summary>Builds the Pilots group used across these fixtures for DAT+UNI.</summary>
    private static AttendeeGroup Pilots() =>
        AttendeeGroup.Define(AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true, TwoTypes);

    private static Attendee NewAttendee() =>
        Attendee.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", Pilots());

    [Fact]
    public void ANewAttendeeStartsNotYetInvited()
    {
        var attendee = NewAttendee();

        Assert.Equal(AttendeeStatus.NotYetInvited, attendee.Status);
        Assert.Equal("Amara Novak", attendee.Name);
        Assert.Equal("a.novak@mail.com", attendee.Email);
    }

    [Fact]
    public void NameAndEmailAreTrimmedAndTheEmailIsLowerCased()
    {
        var attendee = Attendee.Create(
            Guid.NewGuid(), "  Amara Novak  ", "  A.Novak@Mail.COM ", Pilots());

        Assert.Equal("Amara Novak", attendee.Name);
        Assert.Equal("a.novak@mail.com", attendee.Email);
    }

    [Fact]
    public void RequirementsAreRecordedAgainstTheAttendee()
    {
        var attendee = NewAttendee();

        Assert.Equal(2, attendee.Requirements.Count);
        Assert.All(attendee.Requirements, r => Assert.Equal(attendee.Id, r.AttendeeId));
        Assert.Equal(
            TwoTypes.OrderBy(id => id),
            attendee.RequiredAppointmentTypeIds.OrderBy(id => id));
    }

    [Fact]
    public void AMissingNameIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => Attendee.Create(Guid.NewGuid(), "  ", "a.novak@mail.com", Pilots()));
        Assert.Equal("name must not be blank.", ex.Message);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("no@domain")]
    [InlineData("two@@at.com")]
    [InlineData("spaces in@mail.com")]
    [InlineData("")]
    [InlineData(null)]
    public void AnInvalidEmailIsRejected(string? email)
    {
        var ex = Assert.Throws<DomainException>(
            () => Attendee.Create(Guid.NewGuid(), "Amara Novak", email, Pilots()));
        Assert.Equal("email is not a valid email address.", ex.Message);
    }

    [Fact]
    public void AGroupWithNoMappedTypesIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => AttendeeGroup.Define(Guid.NewGuid(), "EMPTY", "Empty", true, []));
        Assert.Equal("An attendee group must map at least one appointment type.", ex.Message);
    }

    [Fact]
    public void DuplicateMappedTypesAreRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => AttendeeGroup.Define(
                Guid.NewGuid(),
                "DUP",
                "Dup",
                true,
                [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.MedicalCheckUp]));
        Assert.Equal("An attendee group cannot map the same appointment type twice.", ex.Message);
    }

    [Fact]
    public void AnUnknownMappedTypeIsRejected()
    {
        Assert.Throws<DomainException>(
            () => AttendeeGroup.Define(Guid.NewGuid(), "UNKNOWN", "Unknown", true, [Guid.NewGuid()]));
    }

    [Fact]
    public void AllThreeAppointmentTypesAreAllowed()
    {
        var cabinCrew = AttendeeGroup.Define(
            AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true, AppointmentTypeIds.All);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", cabinCrew);

        Assert.Equal(3, attendee.Requirements.Count);
    }

    [Fact]
    public void UpdatingDetailsRevalidates()
    {
        var attendee = NewAttendee();

        attendee.UpdateDetails("Amara N. Novak", "amara@mail.com");
        Assert.Equal("Amara N. Novak", attendee.Name);
        Assert.Equal("amara@mail.com", attendee.Email);

        Assert.Throws<DomainException>(() => attendee.UpdateDetails("Amara", "broken"));
        Assert.Equal("amara@mail.com", attendee.Email);
    }

    [Fact]
    public void AssigningADifferentGroupReplacesThePreviousSet()
    {
        var attendee = NewAttendee();
        var groundOps = AttendeeGroup.Define(
            AttendeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT",
            "Ground Operations Agent", true, [AppointmentTypeIds.MedicalCheckUp]);

        Assert.True(attendee.AssignAttendeeGroup(groundOps));
        Assert.Equal([AppointmentTypeIds.MedicalCheckUp], attendee.RequiredAppointmentTypeIds);
    }

    [Fact]
    public void AssigningAnEquivalentGroupPreservesThePreviousSet()
    {
        var attendee = NewAttendee();
        var equivalent = AttendeeGroup.Define(
            Guid.NewGuid(), "PILOTS_EQUIVALENT", "Pilots equivalent", true, TwoTypes);

        Assert.False(attendee.AssignAttendeeGroup(equivalent));
        Assert.Equal(2, attendee.Requirements.Count);
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Candidates/RequirementOverrideSurfaceTests.cs — 1/1

<!-- vocabulary-file: {"id":308,"oldPath":"tests/EventBooking.Domain.Tests/Candidates/RequirementOverrideSurfaceTests.cs","newPath":"tests/EventBooking.Domain.Tests/Attendees/RequirementOverrideSurfaceTests.cs","beforeSha":"8362d30540a3e2213f0d2efb1ba8a0dcef815e36e5e61fa91e0e51e48ecb1cee","afterSha":"5ad03301be712258fe16582488df4bebce725569578e00b1a1f9095dd88f45bb","side":"before","part":1,"parts":1} -->

`````csharp
using System.Reflection;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Candidates;

/// <summary>Prevents public factories from reintroducing unsourced Candidate requirements.</summary>
public sealed class RequirementOverrideSurfaceTests
{
    /// <summary>Candidate has no public factory accepting raw requirement identifiers.</summary>
    [Fact]
    public void CandidateHasNoRawRequirementFactory()
    {
        var raw = typeof(Candidate).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == "Create")
            .SelectMany(method => method.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(IEnumerable<Guid>));

        Assert.False(raw);
    }

    /// <summary>Invite has only named initial and recovery factories.</summary>
    [Fact]
    public void InviteHasNoUnsnapshottedCreateFactory()
    {
        Assert.DoesNotContain(
            typeof(Invite).GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name == "Create");
        Assert.Contains(typeof(Invite).GetMethods(), method => method.Name == "CreateInitial");
        Assert.Contains(typeof(Invite).GetMethods(), method => method.Name == "CreateRecovery");
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Attendees/RequirementOverrideSurfaceTests.cs — 1/1

<!-- vocabulary-file: {"id":308,"oldPath":"tests/EventBooking.Domain.Tests/Candidates/RequirementOverrideSurfaceTests.cs","newPath":"tests/EventBooking.Domain.Tests/Attendees/RequirementOverrideSurfaceTests.cs","beforeSha":"8362d30540a3e2213f0d2efb1ba8a0dcef815e36e5e61fa91e0e51e48ecb1cee","afterSha":"5ad03301be712258fe16582488df4bebce725569578e00b1a1f9095dd88f45bb","side":"after","part":1,"parts":1} -->

`````csharp
using System.Reflection;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Attendees;

/// <summary>Prevents public factories from reintroducing unsourced Attendee requirements.</summary>
public sealed class RequirementOverrideSurfaceTests
{
    /// <summary>Attendee has no public factory accepting raw requirement identifiers.</summary>
    [Fact]
    public void AttendeeHasNoRawRequirementFactory()
    {
        var raw = typeof(Attendee).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == "Create")
            .SelectMany(method => method.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(IEnumerable<Guid>));

        Assert.False(raw);
    }

    /// <summary>Invite has only named initial and recovery factories.</summary>
    [Fact]
    public void InviteHasNoUnsnapshottedCreateFactory()
    {
        Assert.DoesNotContain(
            typeof(Invite).GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name == "Create");
        Assert.Contains(typeof(Invite).GetMethods(), method => method.Name == "CreateInitial");
        Assert.Contains(typeof(Invite).GetMethods(), method => method.Name == "CreateRecovery");
    }
}
`````

## before — tests/EventBooking.Domain.Tests/EmployeeGroups/EmployeeGroupCandidateTests.cs — 1/1

<!-- vocabulary-file: {"id":309,"oldPath":"tests/EventBooking.Domain.Tests/EmployeeGroups/EmployeeGroupCandidateTests.cs","newPath":"tests/EventBooking.Domain.Tests/AttendeeGroups/AttendeeGroupAttendeeTests.cs","beforeSha":"b0fce3d8848bcd2986c9984c99762ca1f4214d3bbccf37028b453d49473f0c4c","afterSha":"156767dfdcdca46316d3b3df83428f815db2a1059c78591e252b70a9b02e1637","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Domain.Tests.EmployeeGroups;

/// <summary>Verifies Employee Group invariants and Candidate requirement derivation.</summary>
public sealed class EmployeeGroupCandidateTests
{
    /// <summary>Every approved mapping produces exactly the Issue 91 requirement set.</summary>
    [Theory]
    [MemberData(nameof(ApprovedMappings))]
    public void ApprovedMappingsAreDerived(
        Guid groupId,
        string code,
        string name,
        Guid[] expected)
    {
        var group = EmployeeGroup.Define(groupId, code, name, true, expected);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", group);

        Assert.Equal(groupId, candidate.EmployeeGroupId);
        Assert.Equal(expected.Order(), candidate.RequiredAppointmentTypeIds.Order());
    }

    /// <summary>Changing between equal mappings changes only the assigned group.</summary>
    [Fact]
    public void SetEquivalentAssignmentPreservesTheMaterializedSet()
    {
        var engineering = EmployeeGroup.Define(
            EmployeeGroupIds.Engineering,
            "ENGINEERING",
            "Engineering",
            true,
            [AppointmentTypeIds.MedicalCheckUp]);
        var groundOperations = EmployeeGroup.Define(
            EmployeeGroupIds.GroundOperationsAgent,
            "GROUND_OPERATIONS_AGENT",
            "Ground Operations Agent",
            true,
            [AppointmentTypeIds.MedicalCheckUp]);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", engineering);

        var changed = candidate.AssignEmployeeGroup(groundOperations);

        Assert.False(changed);
        Assert.Equal(EmployeeGroupIds.GroundOperationsAgent, candidate.EmployeeGroupId);
        Assert.Equal([AppointmentTypeIds.MedicalCheckUp], candidate.RequiredAppointmentTypeIds);
    }

    /// <summary>Inactive, empty, duplicate, and unknown mappings cannot become assignment authority.</summary>
    [Fact]
    public void InvalidReferenceDataIsRejected()
    {
        Assert.Throws<DomainException>(() => EmployeeGroup.Define(
            Guid.NewGuid(), "CABIN_CREW", "Cabin Crew", false,
            [AppointmentTypeIds.DrugAndAlcoholTesting]));
        Assert.Throws<DomainException>(() => EmployeeGroup.Define(
            Guid.NewGuid(), "CABIN_CREW", "Cabin Crew", true, []));
        Assert.Throws<DomainException>(() => EmployeeGroup.Define(
            Guid.NewGuid(), "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.MedicalCheckUp]));
        Assert.Throws<DomainException>(() => EmployeeGroup.Define(
            Guid.NewGuid(), "not-canonical", "Cabin Crew", true, [Guid.NewGuid()]));
    }

    /// <summary>Provides the exact five approved group mappings.</summary>
    public static TheoryData<Guid, string, string, Guid[]> ApprovedMappings => new()
    {
        { EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew",
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting] },
        { EmployeeGroupIds.Pilots, "PILOTS", "Pilots",
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting] },
        { EmployeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT", "Ground Operations Agent",
            [AppointmentTypeIds.MedicalCheckUp] },
        { EmployeeGroupIds.Engineering, "ENGINEERING", "Engineering",
            [AppointmentTypeIds.MedicalCheckUp] },
        { EmployeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES", "Ground Transport Services",
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting] },
    };
}
`````

## after — tests/EventBooking.Domain.Tests/AttendeeGroups/AttendeeGroupAttendeeTests.cs — 1/1

<!-- vocabulary-file: {"id":309,"oldPath":"tests/EventBooking.Domain.Tests/EmployeeGroups/EmployeeGroupCandidateTests.cs","newPath":"tests/EventBooking.Domain.Tests/AttendeeGroups/AttendeeGroupAttendeeTests.cs","beforeSha":"b0fce3d8848bcd2986c9984c99762ca1f4214d3bbccf37028b453d49473f0c4c","afterSha":"156767dfdcdca46316d3b3df83428f815db2a1059c78591e252b70a9b02e1637","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Domain.Tests.AttendeeGroups;

/// <summary>Verifies Attendee Group invariants and Attendee requirement derivation.</summary>
public sealed class AttendeeGroupAttendeeTests
{
    /// <summary>Every approved mapping produces exactly the Issue 91 requirement set.</summary>
    [Theory]
    [MemberData(nameof(ApprovedMappings))]
    public void ApprovedMappingsAreDerived(
        Guid groupId,
        string code,
        string name,
        Guid[] expected)
    {
        var group = AttendeeGroup.Define(groupId, code, name, true, expected);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", group);

        Assert.Equal(groupId, attendee.AttendeeGroupId);
        Assert.Equal(expected.Order(), attendee.RequiredAppointmentTypeIds.Order());
    }

    /// <summary>Changing between equal mappings changes only the assigned group.</summary>
    [Fact]
    public void SetEquivalentAssignmentPreservesTheMaterializedSet()
    {
        var engineering = AttendeeGroup.Define(
            AttendeeGroupIds.Engineering,
            "ENGINEERING",
            "Engineering",
            true,
            [AppointmentTypeIds.MedicalCheckUp]);
        var groundOperations = AttendeeGroup.Define(
            AttendeeGroupIds.GroundOperationsAgent,
            "GROUND_OPERATIONS_AGENT",
            "Ground Operations Agent",
            true,
            [AppointmentTypeIds.MedicalCheckUp]);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", engineering);

        var changed = attendee.AssignAttendeeGroup(groundOperations);

        Assert.False(changed);
        Assert.Equal(AttendeeGroupIds.GroundOperationsAgent, attendee.AttendeeGroupId);
        Assert.Equal([AppointmentTypeIds.MedicalCheckUp], attendee.RequiredAppointmentTypeIds);
    }

    /// <summary>Inactive, empty, duplicate, and unknown mappings cannot become assignment authority.</summary>
    [Fact]
    public void InvalidReferenceDataIsRejected()
    {
        Assert.Throws<DomainException>(() => AttendeeGroup.Define(
            Guid.NewGuid(), "CABIN_CREW", "Cabin Crew", false,
            [AppointmentTypeIds.DrugAndAlcoholTesting]));
        Assert.Throws<DomainException>(() => AttendeeGroup.Define(
            Guid.NewGuid(), "CABIN_CREW", "Cabin Crew", true, []));
        Assert.Throws<DomainException>(() => AttendeeGroup.Define(
            Guid.NewGuid(), "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.MedicalCheckUp]));
        Assert.Throws<DomainException>(() => AttendeeGroup.Define(
            Guid.NewGuid(), "not-canonical", "Cabin Crew", true, [Guid.NewGuid()]));
    }

    /// <summary>Provides the exact five approved group mappings.</summary>
    public static TheoryData<Guid, string, string, Guid[]> ApprovedMappings => new()
    {
        { AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew",
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting] },
        { AttendeeGroupIds.Pilots, "PILOTS", "Pilots",
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting] },
        { AttendeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT", "Ground Operations Agent",
            [AppointmentTypeIds.MedicalCheckUp] },
        { AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering",
            [AppointmentTypeIds.MedicalCheckUp] },
        { AttendeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES", "Ground Transport Services",
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting] },
    };
}
`````

## before — tests/EventBooking.Domain.Tests/Invites/InviteTests.cs — 1/1

<!-- vocabulary-file: {"id":310,"oldPath":"tests/EventBooking.Domain.Tests/Invites/InviteTests.cs","newPath":"tests/EventBooking.Domain.Tests/Invites/InviteTests.cs","beforeSha":"74b496c6098f24acef6b7aef47cba988fdddbccd10aec1487c37efffbe281354","afterSha":"92141fb780fe16ed1a2d551764117e8fdf0212795733d7dab4ffce6fdb519184","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Invites;

public class InviteTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid SlotA = Guid.Parse("50000001-0000-0000-0000-000000000001");
    private static readonly Guid SlotB = Guid.Parse("50000002-0000-0000-0000-000000000002");
    private static readonly Guid SlotC = Guid.Parse("50000003-0000-0000-0000-000000000003");
    private static readonly Guid SlotD = Guid.Parse("50000004-0000-0000-0000-000000000004");

    private static Invite NewInvite(int retryCount = 0) =>
        Invite.CreateInitial(
            Guid.NewGuid(), Guid.NewGuid(), "hash-of-the-token", Now.AddDays(4),
            [SlotA, SlotB, SlotC], [AppointmentTypeIds.DrugAndAlcoholTesting], retryCount);

    [Fact]
    public void ANewInviteIsPendingWithThreeOptions()
    {
        var invite = NewInvite();

        Assert.Equal(InviteStatus.Pending, invite.Status);
        Assert.Equal(3, invite.Options.Count);
        Assert.Equal(Invite.RequiredOptionCount, invite.Options.Count);
        Assert.Equal([SlotA, SlotB, SlotC], invite.OfferedSlotIds);
        Assert.Equal(0, invite.RetryCount);
        Assert.Equal("hash-of-the-token", invite.TokenHash);
    }

    [Fact]
    public void EveryOptionBelongsToTheInvite()
    {
        var invite = NewInvite();

        Assert.All(invite.Options, o => Assert.Equal(invite.Id, o.InviteId));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void AnInviteMustOfferExactlyThreeOptions(int optionCount)
    {
        var slots = new[] { SlotA, SlotB, SlotC, SlotD }.Take(optionCount);

        var ex = Assert.Throws<DomainException>(
            () => Invite.CreateInitial(
                Guid.NewGuid(), Guid.NewGuid(), "hash", Now.AddDays(4),
                slots, [AppointmentTypeIds.DrugAndAlcoholTesting], 0));
        Assert.Equal("An invite must offer exactly 3 slot options.", ex.Message);
    }

    [Fact]
    public void TheSameSlotCannotBeOfferedTwice()
    {
        var ex = Assert.Throws<DomainException>(
            () => Invite.CreateInitial(
                Guid.NewGuid(), Guid.NewGuid(), "hash", Now.AddDays(4),
                [SlotA, SlotA, SlotB], [AppointmentTypeIds.DrugAndAlcoholTesting], 0));
        Assert.Equal("An invite cannot offer the same slot twice.", ex.Message);
    }

    [Fact]
    public void AnInviteWithoutATokenHashIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => Invite.CreateInitial(
                Guid.NewGuid(), Guid.NewGuid(), "  ", Now.AddDays(4),
                [SlotA, SlotB, SlotC], [AppointmentTypeIds.DrugAndAlcoholTesting], 0));
        Assert.Equal("tokenHash must not be blank.", ex.Message);
    }

    [Fact]
    public void ANegativeRetryCountIsRejected()
    {
        Assert.Throws<DomainException>(
            () => Invite.CreateInitial(
                Guid.NewGuid(), Guid.NewGuid(), "hash", Now.AddDays(4),
                [SlotA, SlotB, SlotC], [AppointmentTypeIds.DrugAndAlcoholTesting], -1));
    }

    [Fact]
    public void APendingInviteIsUsableUntilItExpires()
    {
        var invite = NewInvite();

        Assert.True(invite.IsUsableAt(Now));
        Assert.True(invite.IsUsableAt(Now.AddDays(4).AddSeconds(-1)));
        Assert.False(invite.IsUsableAt(Now.AddDays(4)));
        Assert.False(invite.IsUsableAt(Now.AddDays(5)));
    }

    [Fact]
    public void AUsedInviteIsNeverUsableAgain()
    {
        var invite = NewInvite();
        invite.MarkUsed();

        Assert.Equal(InviteStatus.Used, invite.Status);
        Assert.False(invite.IsUsableAt(Now));
    }

    [Fact]
    public void OnlyAPendingInviteCanBeUsedExpiredOrSuperseded()
    {
        var used = NewInvite();
        used.MarkUsed();
        Assert.Throws<DomainException>(() => used.MarkExpired());
        Assert.Throws<DomainException>(() => used.MarkSuperseded());
        Assert.Throws<DomainException>(() => used.MarkUsed());

        var expired = NewInvite();
        expired.MarkExpired();
        Assert.Equal(InviteStatus.Expired, expired.Status);
        Assert.Throws<DomainException>(() => expired.MarkUsed());

        var superseded = NewInvite();
        superseded.MarkSuperseded();
        Assert.Equal(InviteStatus.Superseded, superseded.Status);
    }

    [Fact]
    public void AnOptionThatFilledUpIsDroppedAndAReplacementRestoresThree()
    {
        var invite = NewInvite();

        invite.RemoveOption(SlotB);
        Assert.Equal(2, invite.Options.Count);
        Assert.False(invite.Offers(SlotB));

        invite.AddOption(SlotD);
        Assert.Equal(3, invite.Options.Count);
        Assert.True(invite.Offers(SlotD));
    }

    [Fact]
    public void AFourthOptionIsRejected()
    {
        var invite = NewInvite();

        var ex = Assert.Throws<DomainException>(() => invite.AddOption(SlotD));
        Assert.Equal("An invite cannot offer more than 3 slot options.", ex.Message);
    }

    [Fact]
    public void AddingAnOptionAlreadyOfferedIsRejected()
    {
        var invite = NewInvite();
        invite.RemoveOption(SlotB);

        var ex = Assert.Throws<DomainException>(() => invite.AddOption(SlotA));
        Assert.Equal("An invite cannot offer the same slot twice.", ex.Message);
    }

    [Fact]
    public void RemovingAnOptionThatWasNotOfferedIsRejected()
    {
        var invite = NewInvite();

        var ex = Assert.Throws<DomainException>(() => invite.RemoveOption(SlotD));
        Assert.Equal("This invite does not offer that slot.", ex.Message);
    }

    [Fact]
    public void OptionsCanOnlyChangeWhileTheInviteIsPending()
    {
        var invite = NewInvite();
        invite.MarkUsed();

        Assert.Throws<DomainException>(() => invite.RemoveOption(SlotA));
        Assert.Throws<DomainException>(() => invite.AddOption(SlotD));
    }

    [Fact]
    public void TheRetryCountIsCarriedForwardByTheCaller()
    {
        var invite = NewInvite(retryCount: 2);

        Assert.Equal(2, invite.RetryCount);
    }
}
`````
