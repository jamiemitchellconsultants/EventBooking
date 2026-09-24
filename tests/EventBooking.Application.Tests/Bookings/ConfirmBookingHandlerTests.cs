using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Tests.Bookings;

public sealed class ConfirmBookingHandlerTests
{
    private static ConfirmBookingHandler Handler(BookingFixture f) => new(
        f.Invites, f.Attendees, f.Events, f.Capacities, f.Bookings, f.Appointments,
        f.Locations, f.Tokens, f.Emails, f.UnitOfWork, f.Audit, f.Clock,
        BookingTestZones.Instance);

    [Fact]
    public async Task Confirm_charges_only_required_types_and_stages_confirmation()
    {
        var fixture = BookingFixture.Create();
        var (attendeeId, inviteId) = fixture.InviteAttendee("IND");
        var invite = fixture.Invites.Items.Single(i => i.Id == inviteId);
        // The invite itself stages one email and one audit entry; the assertions below pin
        // only what the confirmation adds.
        fixture.Emails.Items.Clear();
        fixture.Audit.Entries.Clear();

        var result = await Handler(fixture).HandleAsync(
            new ConfirmBookingCommand(fixture.BookTokenFor(inviteId, invite.TokenVersion), fixture.EventId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(9, fixture.RemainingFor("IND"));
        Assert.Equal(10, fixture.RemainingFor("MED"));
        Assert.Equal(10, fixture.RemainingFor("FIT"));
        var booking = Assert.Single(fixture.Bookings.Items);
        Assert.Equal(attendeeId, booking.AttendeeId);
        Assert.Single(fixture.Appointments.Items);
        Assert.Equal(Domain.Invites.InviteStatus.Used, invite.Status);
        Assert.Equal("Booked", fixture.Attendees.Items.Single(a => a.Id == attendeeId).Status.ToString());
        var delivery = Assert.Single(fixture.Emails.Items);
        Assert.Equal(EmailTemplate.BookingConfirmation, delivery.TemplateName);
        Assert.Equal(booking.Id, delivery.BookingId);
        Assert.Equal(
            [AuditAction.BookingCreated, AuditAction.CapacityDecremented],
            fixture.Audit.Entries.Select(e => e.Action).ToList());
        Assert.Equal(
            fixture.ManageTokenFor(booking.Id, booking.ManageTokenVersion), result.Value.ManageToken);
    }

    [Fact]
    public async Task Confirm_refuses_an_invite_that_expired_but_was_not_yet_swept()
    {
        var fixture = BookingFixture.Create();
        var (_, inviteId) = fixture.InviteAttendee("IND");
        var invite = fixture.Invites.Items.Single(i => i.Id == inviteId);
        fixture.Clock.UtcNow = invite.ExpiresAt.AddMinutes(1);

        var result = await Handler(fixture).HandleAsync(
            new ConfirmBookingCommand(fixture.BookTokenFor(inviteId, invite.TokenVersion), fixture.EventId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(fixture.Bookings.Items);
        Assert.Equal(Domain.Invites.InviteStatus.Pending, invite.Status);
    }

    [Fact]
    public async Task Replay_returns_conflict_naming_existing_booking_with_no_second_row()
    {
        var fixture = BookingFixture.Create();
        var (_, inviteId) = fixture.InviteAttendee("IND");
        var invite = fixture.Invites.Items.Single(i => i.Id == inviteId);
        var first = await Handler(fixture).HandleAsync(
            new ConfirmBookingCommand(fixture.BookTokenFor(inviteId, invite.TokenVersion), fixture.EventId),
            CancellationToken.None);
        Assert.True(first.IsSuccess);

        var result = await Handler(fixture).HandleAsync(
            new ConfirmBookingCommand(fixture.BookTokenFor(inviteId, invite.TokenVersion), fixture.EventId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("already-confirmed", result.Error.Code);
        Assert.Contains(first.Value.BookingId.ToString(), result.Error.Message);
        Assert.Single(fixture.Bookings.Items);
        Assert.Equal(9, fixture.RemainingFor("IND"));
    }

    [Fact]
    public async Task Exhausted_required_type_changes_nothing()
    {
        var fixture = BookingFixture.Create();
        fixture.Occupy("IND", 10);
        var (_, inviteId) = fixture.InviteAttendee("MED", "IND");
        var invite = fixture.Invites.Items.Single(i => i.Id == inviteId);

        var result = await Handler(fixture).HandleAsync(
            new ConfirmBookingCommand(fixture.BookTokenFor(inviteId, invite.TokenVersion), fixture.EventId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("capacity-exhausted", result.Error.Code);
        Assert.Empty(fixture.Bookings.Items);
        Assert.Equal(10, fixture.RemainingFor("MED"));
        Assert.Equal(0, fixture.RemainingFor("IND"));
    }

    [Fact]
    public async Task Tampered_token_is_refused()
    {
        var fixture = BookingFixture.Create();

        var result = await Handler(fixture).HandleAsync(
            new ConfirmBookingCommand("not-a-token", fixture.EventId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("token-invalid", result.Error.Code);
        Assert.Empty(fixture.Bookings.Items);
    }
}
