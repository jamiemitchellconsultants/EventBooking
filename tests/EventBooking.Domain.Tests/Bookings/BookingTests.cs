using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Bookings;

public class BookingTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid SlotA = Guid.Parse("50000001-0000-0000-0000-000000000001");
    private static readonly Guid SlotB = Guid.Parse("50000002-0000-0000-0000-000000000002");
    private static readonly Guid SlotC = Guid.Parse("50000003-0000-0000-0000-000000000003");
    private static readonly Guid SlotNotOffered = Guid.Parse("50000009-0000-0000-0000-000000000009");

    private static Invite NewInvite() =>
        Invite.CreateInitial(
            Guid.NewGuid(), Guid.NewGuid(), "invite-token-hash", Now.AddDays(4),
            [SlotA, SlotB, SlotC], [AppointmentTypeIds.DrugAndAlcoholTesting], 0);

    private static Booking NewBooking(Invite invite) =>
        Booking.Create(Guid.NewGuid(), invite, SlotB, "manage-token-hash", Now);

    [Fact]
    public void ABookingCarriesTheCandidateSlotAndInvite()
    {
        var invite = NewInvite();

        var booking = NewBooking(invite);

        Assert.Equal(invite.CandidateId, booking.CandidateId);
        Assert.Equal(SlotB, booking.ConfirmedSlotId);
        Assert.Equal(invite.Id, booking.InviteId);
        Assert.Equal(Now, booking.CreatedAt);
        Assert.Equal(BookingStatus.Active, booking.Status);
        Assert.Equal("manage-token-hash", booking.ManageTokenHash);
    }

    [Fact]
    public void BookingASlotTheInviteNeverOfferedIsRejected()
    {
        var invite = NewInvite();

        var ex = Assert.Throws<DomainException>(
            () => Booking.Create(Guid.NewGuid(), invite, SlotNotOffered, "manage-token-hash", Now));
        Assert.Equal("The chosen slot is not one of this invite's options.", ex.Message);
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
            () => Booking.Create(Guid.NewGuid(), invite, SlotB, " ", Now));
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
