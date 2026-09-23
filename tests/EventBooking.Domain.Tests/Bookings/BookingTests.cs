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
            Now.AddDays(4),
            [ProposalFixture.LocationId],
            [EventA, EventB, EventC],
            [AppointmentTypeIds.DrugAndAlcoholTesting],
            0);

    private static Booking NewBooking(Invite invite) =>
        Booking.Create(Guid.NewGuid(), invite, EventB, Now);

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
        Assert.Equal(Booking.InitialManageTokenVersion, booking.ManageTokenVersion);
    }

    [Fact]
    public void BookingAEventTheInviteNeverOfferedIsRejected()
    {
        var invite = NewInvite();

        var ex = Assert.Throws<DomainException>(
            () => Booking.Create(Guid.NewGuid(), invite, EventNotOffered, Now));
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
    public void RotatingAnActiveBookingsManageTokenMovesToTheNextVersion()
    {
        var booking = NewBooking(NewInvite());

        booking.RotateManageToken();

        Assert.Equal(Booking.InitialManageTokenVersion + 1, booking.ManageTokenVersion);
    }

    [Fact]
    public void RotatingTheManageTokenOfACancelledBookingIsRejected()
    {
        var booking = NewBooking(NewInvite());
        booking.Cancel();

        var ex = Assert.Throws<DomainException>(booking.RotateManageToken);
        Assert.Equal("Only an active booking token can be rotated.", ex.Message);
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
