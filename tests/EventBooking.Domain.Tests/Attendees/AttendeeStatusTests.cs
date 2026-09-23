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
