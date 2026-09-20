# 01e — Location-restricted invites and closed attendee transitions, edits 25 (Task 8)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Domain.Tests/Attendees/AttendeeStatusTransitionTests.cs — 1/1

<!-- retirement-file: {"id":65,"file":"tests/EventBooking.Domain.Tests/Attendees/AttendeeStatusTransitionTests.cs","beforeSha":null,"afterSha":"d59bfad8d1f4bce62f3ae0b773dbfcee4710a8517a1e5b7324becc3c9412a7d5","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.Attendees;

/// <summary>
/// Task 8: the attendee status table is closed. Every (from, to) pair is enumerated here, and only
/// the rows design 01 lists succeed; everything else is refused and leaves the attendee alone
/// (decision D15). Every accepted change stamps statusChangedAt.
/// </summary>
public class AttendeeStatusTransitionTests
{
    private static readonly DateTimeOffset Created = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Every row of design 01's table, as (from, to) pairs.</summary>
    private static readonly HashSet<(AttendeeStatus From, AttendeeStatus To)> Legal =
    [
        (AttendeeStatus.NotYetInvited, AttendeeStatus.Invited),
        (AttendeeStatus.NotYetInvited, AttendeeStatus.AwaitingAvailability),
        (AttendeeStatus.AwaitingAvailability, AttendeeStatus.Invited),
        (AttendeeStatus.AwaitingAvailability, AttendeeStatus.NotYetInvited),
        (AttendeeStatus.Invited, AttendeeStatus.Invited),
        (AttendeeStatus.Invited, AttendeeStatus.Booked),
        (AttendeeStatus.Invited, AttendeeStatus.NoResponseNeedsFollowUp),
        (AttendeeStatus.Invited, AttendeeStatus.NotYetInvited),
        (AttendeeStatus.NoResponseNeedsFollowUp, AttendeeStatus.Invited),
        (AttendeeStatus.NoResponseNeedsFollowUp, AttendeeStatus.AwaitingAvailability),
        (AttendeeStatus.NoResponseNeedsFollowUp, AttendeeStatus.NotYetInvited),
        (AttendeeStatus.Booked, AttendeeStatus.Invited),
        (AttendeeStatus.Booked, AttendeeStatus.AwaitingAvailability),
        (AttendeeStatus.Booked, AttendeeStatus.NotYetInvited),
    ];

    public static TheoryData<AttendeeStatus, AttendeeStatus> EveryPair()
    {
        var data = new TheoryData<AttendeeStatus, AttendeeStatus>();
        foreach (var from in Enum.GetValues<AttendeeStatus>())
        {
            foreach (var to in Enum.GetValues<AttendeeStatus>())
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryPair))]
    public void ExactlyTheListedTransitionsAreAccepted(AttendeeStatus from, AttendeeStatus to)
    {
        var attendee = AttendeeIn(from);
        var expected = Legal.Contains((from, to));

        Assert.Equal(expected, Attendee.IsLegalTransition(from, to));

        if (expected)
        {
            MoveTo(attendee, to, Later);

            Assert.Equal(to, attendee.Status);
            Assert.Equal(Later, attendee.StatusChangedAt);
            return;
        }

        var exception = Assert.Throws<DomainException>(() => MoveTo(attendee, to, Later));

        Assert.Equal($"A attendee cannot move from {from} to {to}.", exception.Message);
        Assert.Equal(from, attendee.Status);
        Assert.Equal(Created, attendee.StatusChangedAt);
    }

    [Fact]
    public void CreationStampsTheStatusChangeInstant()
    {
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", DatOnly(), Created);

        Assert.Equal(AttendeeStatus.NotYetInvited, attendee.Status);
        Assert.Equal(Created, attendee.StatusChangedAt);
    }

    [Fact]
    public void ARequirementChangeResetsAnUnbookedAttendeeButRefusesABookedOne()
    {
        var unbooked = AttendeeIn(AttendeeStatus.Invited);

        unbooked.ResetAfterRequirementChange(Later);

        Assert.Equal(AttendeeStatus.NotYetInvited, unbooked.Status);
        Assert.Equal(Later, unbooked.StatusChangedAt);

        var booked = AttendeeIn(AttendeeStatus.Booked);

        Assert.Throws<DomainException>(() => booked.ResetAfterRequirementChange(Later));
        Assert.Equal(AttendeeStatus.Booked, booked.Status);
    }

    [Fact]
    public void ARequirementChangeLeavesANeverInvitedAttendeeUntouched()
    {
        var attendee = AttendeeIn(AttendeeStatus.NotYetInvited);

        attendee.ResetAfterRequirementChange(Later);

        Assert.Equal(AttendeeStatus.NotYetInvited, attendee.Status);
        Assert.Equal(Created, attendee.StatusChangedAt);
    }

    private static AttendeeGroup DatOnly() =>
        AttendeeGroup.Define(
            Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);

    /// <summary>Walks an attendee to the wanted origin using only legal moves, stamped at creation.</summary>
    private static Attendee AttendeeIn(AttendeeStatus status)
    {
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", DatOnly(), Created);

        switch (status)
        {
            case AttendeeStatus.NotYetInvited:
                break;
            case AttendeeStatus.AwaitingAvailability:
                attendee.MarkAwaitingAvailability(Created);
                break;
            case AttendeeStatus.Invited:
                attendee.MarkInvited(Created);
                break;
            case AttendeeStatus.Booked:
                attendee.MarkInvited(Created);
                attendee.MarkBooked(Created);
                break;
            case AttendeeStatus.NoResponseNeedsFollowUp:
                attendee.MarkInvited(Created);
                attendee.MarkNoResponse(Created);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        return attendee;
    }

    private static void MoveTo(Attendee attendee, AttendeeStatus target, DateTimeOffset now)
    {
        switch (target)
        {
            case AttendeeStatus.NotYetInvited:
                attendee.ResetToNotYetInvited(now);
                break;
            case AttendeeStatus.AwaitingAvailability:
                attendee.MarkAwaitingAvailability(now);
                break;
            case AttendeeStatus.Invited:
                attendee.MarkInvited(now);
                break;
            case AttendeeStatus.Booked:
                attendee.MarkBooked(now);
                break;
            case AttendeeStatus.NoResponseNeedsFollowUp:
                attendee.MarkNoResponse(now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target));
        }
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Attendees/AttendeeTests.cs — 1/1

<!-- retirement-file: {"id":66,"file":"tests/EventBooking.Domain.Tests/Attendees/AttendeeTests.cs","beforeSha":"96b980c36a7d4539ea53aaa44fa9c650bfb0c815697dd9521fede394d0dcab98","afterSha":"51186f640bbb4f4eab177bf844b360eed3d525fa83e9677b7eff11f22dddc541","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Domain.Tests/Attendees/AttendeeTests.cs — 1/1

<!-- retirement-file: {"id":66,"file":"tests/EventBooking.Domain.Tests/Attendees/AttendeeTests.cs","beforeSha":"96b980c36a7d4539ea53aaa44fa9c650bfb0c815697dd9521fede394d0dcab98","afterSha":"51186f640bbb4f4eab177bf844b360eed3d525fa83e9677b7eff11f22dddc541","side":"after","part":1,"parts":1} -->

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
        Attendee.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", Pilots(), ProposalFixture.Now);

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
            Guid.NewGuid(),
            "  Amara Novak  ",
            "  A.Novak@Mail.COM ",
            Pilots(),
            ProposalFixture.Now);

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
            () => Attendee.Create(Guid.NewGuid(), "  ", "a.novak@mail.com", Pilots(), ProposalFixture.Now));
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
            () => Attendee.Create(Guid.NewGuid(), "Amara Novak", email, Pilots(), ProposalFixture.Now));
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
            Guid.NewGuid(),
            "Amara Novak",
            "a.novak@mail.com",
            cabinCrew,
            ProposalFixture.Now);

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

## before — tests/EventBooking.Domain.Tests/Attendees/RequiredAttendeeGroupTests.cs — 1/1

<!-- retirement-file: {"id":67,"file":"tests/EventBooking.Domain.Tests/Attendees/RequiredAttendeeGroupTests.cs","beforeSha":"bf29699d2d6fdf72a3f189d825de0594a813d6aa1d66e65a74b84b25c17b76e0","afterSha":"aabb4d39d6e9a3a4710e0f8e55e16fe78a7debd5521c9a0b44737273e51f5351","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Attendees;

namespace EventBooking.Domain.Tests.Attendees;

public sealed class RequiredAttendeeGroupTests
{
    [Fact]
    public void Creation_without_a_group_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Attendee.Create(Guid.NewGuid(), "Demo attendee", "demo@example.test", null!));
    }

    [Fact]
    public void Aggregate_does_not_expose_a_nullable_group_identifier()
    {
        Assert.Equal(typeof(Guid), typeof(Attendee).GetProperty(nameof(Attendee.AttendeeGroupId))!.PropertyType);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Attendees/RequiredAttendeeGroupTests.cs — 1/1

<!-- retirement-file: {"id":67,"file":"tests/EventBooking.Domain.Tests/Attendees/RequiredAttendeeGroupTests.cs","beforeSha":"bf29699d2d6fdf72a3f189d825de0594a813d6aa1d66e65a74b84b25c17b76e0","afterSha":"aabb4d39d6e9a3a4710e0f8e55e16fe78a7debd5521c9a0b44737273e51f5351","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Attendees;

namespace EventBooking.Domain.Tests.Attendees;

public sealed class RequiredAttendeeGroupTests
{
    [Fact]
    public void Creation_without_a_group_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Attendee.Create(Guid.NewGuid(), "Demo attendee", "demo@example.test", null!, ProposalFixture.Now));
    }

    [Fact]
    public void Aggregate_does_not_expose_a_nullable_group_identifier()
    {
        Assert.Equal(typeof(Guid), typeof(Attendee).GetProperty(nameof(Attendee.AttendeeGroupId))!.PropertyType);
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs — 1/1

<!-- retirement-file: {"id":68,"file":"tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs","beforeSha":"676c1d2df6d57896fefeda6026b3526b874a5c245cd333de0c04e62cc1eccb48","afterSha":"6f0354359eb447e30668a1bbc4ae266a4e85293f144cde7f5fb15636b75dd57b","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs — 1/1

<!-- retirement-file: {"id":68,"file":"tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs","beforeSha":"676c1d2df6d57896fefeda6026b3526b874a5c245cd333de0c04e62cc1eccb48","afterSha":"6f0354359eb447e30668a1bbc4ae266a4e85293f144cde7f5fb15636b75dd57b","side":"after","part":1,"parts":1} -->

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
            Guid.NewGuid(),
            Guid.NewGuid(),
            "invite-token-hash",
            Now.AddDays(4),
            [ProposalFixture.LocationId],
            [EventA, EventB, EventC],
            [AppointmentTypeIds.DrugAndAlcoholTesting],
            0);

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

<!-- retirement-file: {"id":69,"file":"tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs","beforeSha":"baf99bb238b154c6c98372c80e77e84dd001924e45ba9c0ecddc72924e84004f","afterSha":"7cdf471358f30e9215924c5a0058bb52f7df9b165aac6b7fdb12da8925a0c3bf","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs — 1/1

<!-- retirement-file: {"id":69,"file":"tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs","beforeSha":"baf99bb238b154c6c98372c80e77e84dd001924e45ba9c0ecddc72924e84004f","afterSha":"7cdf471358f30e9215924c5a0058bb52f7df9b165aac6b7fdb12da8925a0c3bf","side":"after","part":1,"parts":1} -->

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
            Guid.NewGuid(),
            attendeeId,
            "initial",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        var original = Booking.Create(
            Guid.NewGuid(), initialInvite, eventId, "manage-original", DateTimeOffset.UtcNow);
        var recoveryEvent = Guid.NewGuid();
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            attendeeId,
            original.Id,
            "recovery",
            DateTimeOffset.UtcNow.AddDays(2),
            ProposalFixture.LocationId,
            null,
            [recoveryEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);

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
            Guid.NewGuid(),
            attendeeId,
            "initial",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [rootEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        var root = Booking.Create(Guid.NewGuid(), initial, rootEvent, "root", DateTimeOffset.UtcNow);
        var firstEvent = Guid.NewGuid();
        var firstInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            attendeeId,
            root.Id,
            "first",
            DateTimeOffset.UtcNow.AddDays(1),
            ProposalFixture.LocationId,
            null,
            [firstEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);
        var first = Booking.CreateRecovery(
            Guid.NewGuid(), firstInvite, root, firstEvent, "first-manage", DateTimeOffset.UtcNow);
        var secondEvent = Guid.NewGuid();
        var secondInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            attendeeId,
            root.Id,
            "second",
            DateTimeOffset.UtcNow.AddDays(1),
            ProposalFixture.LocationId,
            null,
            [secondEvent, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);

        Assert.Throws<EventBooking.Domain.Common.DomainException>(() => Booking.CreateRecovery(
            Guid.NewGuid(), secondInvite, first, secondEvent, "second-manage", DateTimeOffset.UtcNow));
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Invites/InviteLocationTests.cs — 1/1

<!-- retirement-file: {"id":70,"file":"tests/EventBooking.Domain.Tests/Invites/InviteLocationTests.cs","beforeSha":null,"afterSha":"ce178527b6e4e05d4bbe8993d4d8a28b0134326b8a0ac727493263e1bab89984","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Invites;

/// <summary>
/// Task 8: an invite is restricted to the locations the Coordinator chose, and every later offer
/// is drawn from that same set (FR-5.1, FR-5.2, FR-5.6, FR-5.9).
/// </summary>
public class InviteLocationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid London = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Dublin = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid Tokyo = Guid.Parse("10000000-0000-0000-0000-000000000003");

    private static readonly Guid EventA = Guid.Parse("50000001-0000-0000-0000-000000000001");
    private static readonly Guid EventB = Guid.Parse("50000002-0000-0000-0000-000000000002");
    private static readonly Guid EventC = Guid.Parse("50000003-0000-0000-0000-000000000003");
    private static readonly Guid EventD = Guid.Parse("50000004-0000-0000-0000-000000000004");
    private static readonly Guid EventE = Guid.Parse("50000005-0000-0000-0000-000000000005");
    private static readonly Guid EventF = Guid.Parse("50000006-0000-0000-0000-000000000006");

    private static Invite Initial(IEnumerable<Guid>? locationIds = null, int retryCount = 0) =>
        Invite.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hash-of-the-token",
            Now.AddDays(4),
            locationIds ?? [London, Dublin],
            [EventA, EventB, EventC],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp],
            retryCount);

    [Fact]
    public void AnInviteSnapshotsTheChosenLocations()
    {
        var invite = Initial();

        Assert.Equal([London, Dublin], invite.LocationIds.Order());
        Assert.All(invite.Locations, location => Assert.Equal(invite.Id, location.InviteId));
    }

    [Fact]
    public void AnInviteWithNoLocationsIsRefused()
    {
        var exception = Assert.Throws<DomainException>(() => Initial([]));

        Assert.Contains("at least one location", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInviteWithMoreThanFiftyLocationsIsRefused()
    {
        var tooMany = Enumerable.Range(0, Invite.MaximumLocationCount + 1)
            .Select(_ => Guid.NewGuid())
            .ToList();

        var exception = Assert.Throws<DomainException>(() => Initial(tooMany));

        Assert.Contains(
            Invite.MaximumLocationCount.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FiftyLocationsIsAccepted()
    {
        var most = Enumerable.Range(0, Invite.MaximumLocationCount)
            .Select(_ => Guid.NewGuid())
            .ToList();

        Assert.Equal(Invite.MaximumLocationCount, Initial(most).Locations.Count);
    }

    [Fact]
    public void ARepeatedLocationIsRefused()
    {
        var exception = Assert.Throws<DomainException>(() => Initial([London, London]));

        Assert.Contains("same location twice", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AReissueCarriesTheLocationSetAndCountsTheRetry()
    {
        var original = Initial([London, Dublin, Tokyo], retryCount: 1);

        var reissued = Invite.Reissue(
            Guid.NewGuid(), original, "hash-of-the-next-token", Now.AddDays(11),
            [EventD, EventE, EventF]);

        Assert.Equal(original.LocationIds.Order(), reissued.LocationIds.Order());
        Assert.Equal(original.AttendeeId, reissued.AttendeeId);
        Assert.Equal(original.RequiredAppointmentTypeIds, reissued.RequiredAppointmentTypeIds);
        Assert.Equal(2, reissued.RetryCount);
        Assert.Equal(InviteStatus.Pending, reissued.Status);
        Assert.Equal([EventD, EventE, EventF], reissued.OfferedEventIds);
        Assert.All(reissued.Locations, location => Assert.Equal(reissued.Id, location.InviteId));
    }

    [Fact]
    public void AReissueOfARecoveryInviteStaysARecoveryInvite()
    {
        var bookingId = Guid.NewGuid();
        var original = Recovery(bookingId);

        var reissued = Invite.Reissue(
            Guid.NewGuid(), original, "hash-of-the-next-token", Now.AddDays(11),
            [EventD, EventE, EventF]);

        Assert.Equal(bookingId, reissued.RecoveryOfBookingId);
        Assert.Equal(1, reissued.RetryCount);
    }

    [Fact]
    public void ARecoveryInviteDefaultsToTheOriginalBookingsLocation()
    {
        var invite = Recovery(Guid.NewGuid());

        Assert.Equal([London], invite.LocationIds);
        Assert.Equal(0, invite.RetryCount);
    }

    [Fact]
    public void ARecoveryInviteUnionsAdditionalLocationsWithoutDuplicates()
    {
        var invite = Recovery(Guid.NewGuid(), [Dublin, London, Tokyo, Dublin]);

        Assert.Equal([London, Dublin, Tokyo], invite.LocationIds.Order());
    }

    [Fact]
    public void AnInviteSnapshotsMoreThanThreeRequirements()
    {
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hash-of-the-token",
            Now.AddDays(4),
            [London],
            [EventA, EventB, EventC],
            AppointmentTypeIds.All,
            0);

        Assert.Equal(AppointmentTypeIds.All.Order(), invite.RequiredAppointmentTypeIds);
    }

    private static Invite Recovery(Guid bookingId, IEnumerable<Guid>? additionalLocationIds = null) =>
        Invite.CreateRecovery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            bookingId,
            "hash-of-the-token",
            Now.AddDays(4),
            London,
            additionalLocationIds,
            [EventA, EventB, EventC],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
}
`````

## before — tests/EventBooking.Domain.Tests/Invites/InviteRequirementSnapshotTests.cs — 1/1

<!-- retirement-file: {"id":71,"file":"tests/EventBooking.Domain.Tests/Invites/InviteRequirementSnapshotTests.cs","beforeSha":"f97f43739cfeac43ad7db68c305f4481cf76d025c12ae99fc28f7e6257eab564","afterSha":"a99b3194121189784c72d0e5f72f24fcd378551ba41488bf954e9448d0806eaf","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Invites;

/// <summary>Verifies initial and recovery Invites own immutable requirement snapshots.</summary>
public sealed class InviteRequirementSnapshotTests
{
    private static readonly Guid[] Options = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

    /// <summary>An initial Invite snapshots distinct known requirements in stable order.</summary>
    [Fact]
    public void InitialInviteSnapshotsRequirements()
    {
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), Guid.NewGuid(), "hash", DateTimeOffset.UtcNow.AddDays(1), Options,
            [AppointmentTypeIds.UniformFitting, AppointmentTypeIds.DrugAndAlcoholTesting], 0);

        Assert.Null(invite.RecoveryOfBookingId);
        Assert.Equal(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            invite.RequiredAppointmentTypeIds);
    }

    /// <summary>A recovery Invite retains its root Booking and can be explicitly cancelled.</summary>
    [Fact]
    public void RecoveryInviteLinksTheRootAndCancels()
    {
        var root = Guid.NewGuid();
        var invite = Invite.CreateRecovery(
            Guid.NewGuid(), Guid.NewGuid(), root, "hash", DateTimeOffset.UtcNow.AddDays(1), Options,
            [AppointmentTypeIds.MedicalCheckUp]);

        invite.CancelRecovery();

        Assert.Equal(root, invite.RecoveryOfBookingId);
        Assert.Equal(InviteStatus.Cancelled, invite.Status);
    }

    /// <summary>Empty, duplicate, unknown, and oversized snapshots are rejected.</summary>
    [Theory]
    [MemberData(nameof(InvalidSnapshots))]
    public void InvalidSnapshotsCannotBeCreated(Guid[] snapshot)
    {
        Assert.Throws<DomainException>(() => Invite.CreateInitial(
            Guid.NewGuid(), Guid.NewGuid(), "hash", DateTimeOffset.UtcNow.AddDays(1), Options,
            snapshot, 0));
    }

    /// <summary>Provides every invalid snapshot shape.</summary>
    public static TheoryData<Guid[]> InvalidSnapshots => new()
    {
        { [] },
        { [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.MedicalCheckUp] },
        { [Guid.NewGuid()] },
        { [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
            AppointmentTypeIds.UniformFitting, AppointmentTypeIds.DrugAndAlcoholTesting] },
    };
}
`````

## after — tests/EventBooking.Domain.Tests/Invites/InviteRequirementSnapshotTests.cs — 1/1

<!-- retirement-file: {"id":71,"file":"tests/EventBooking.Domain.Tests/Invites/InviteRequirementSnapshotTests.cs","beforeSha":"f97f43739cfeac43ad7db68c305f4481cf76d025c12ae99fc28f7e6257eab564","afterSha":"a99b3194121189784c72d0e5f72f24fcd378551ba41488bf954e9448d0806eaf","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Invites;

/// <summary>Verifies initial and recovery Invites own immutable requirement snapshots.</summary>
public sealed class InviteRequirementSnapshotTests
{
    private static readonly Guid[] Options = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

    /// <summary>An initial Invite snapshots distinct known requirements in stable order.</summary>
    [Fact]
    public void InitialInviteSnapshotsRequirements()
    {
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hash",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            Options,
            [AppointmentTypeIds.UniformFitting, AppointmentTypeIds.DrugAndAlcoholTesting],
            0);

        Assert.Null(invite.RecoveryOfBookingId);
        Assert.Equal(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            invite.RequiredAppointmentTypeIds);
    }

    /// <summary>A recovery Invite retains its root Booking and can be explicitly cancelled.</summary>
    [Fact]
    public void RecoveryInviteLinksTheRootAndCancels()
    {
        var root = Guid.NewGuid();
        var invite = Invite.CreateRecovery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            root,
            "hash",
            DateTimeOffset.UtcNow.AddDays(1),
            ProposalFixture.LocationId,
            null,
            Options,
            [AppointmentTypeIds.MedicalCheckUp]);

        invite.CancelRecovery();

        Assert.Equal(root, invite.RecoveryOfBookingId);
        Assert.Equal(InviteStatus.Cancelled, invite.Status);
    }

    /// <summary>Empty, duplicate, unknown, and oversized snapshots are rejected.</summary>
    [Theory]
    [MemberData(nameof(InvalidSnapshots))]
    public void InvalidSnapshotsCannotBeCreated(Guid[] snapshot)
    {
        Assert.Throws<DomainException>(() => Invite.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hash",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            Options,
            snapshot,
            0));
    }

    /// <summary>Provides every invalid snapshot shape.</summary>
    public static TheoryData<Guid[]> InvalidSnapshots => new()
    {
        { [] },
        { [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.MedicalCheckUp] },
        { [Guid.NewGuid()] },
        { [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
            AppointmentTypeIds.UniformFitting, AppointmentTypeIds.DrugAndAlcoholTesting] },
    };
}
`````
