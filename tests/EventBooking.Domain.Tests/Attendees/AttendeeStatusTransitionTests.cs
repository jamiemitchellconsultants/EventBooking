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
