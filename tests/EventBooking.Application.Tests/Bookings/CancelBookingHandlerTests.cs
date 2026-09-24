using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Tests.Bookings;

public sealed class CancelBookingHandlerTests
{
    private static async Task<Guid> ConfirmedBookingAsync(BookingFixture fixture, params string[] codes)
    {
        var (attendeeId, inviteId) = fixture.InviteAttendee(codes);
        var invite = fixture.Invites.Items.Single(i => i.Id == inviteId);
        var confirmer = new ConfirmBookingHandler(
            fixture.Invites, fixture.Attendees, fixture.Events, fixture.Capacities,
            fixture.Bookings, fixture.Appointments, fixture.Locations, fixture.Tokens,
            fixture.Emails, fixture.UnitOfWork, fixture.Audit, fixture.Clock,
            BookingTestZones.Instance);
        var result = await confirmer.HandleAsync(
            new ConfirmBookingCommand(fixture.BookTokenFor(inviteId, invite.TokenVersion), fixture.EventId),
            CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value.BookingId;
    }

    [Fact]
    public async Task Attendee_cancel_without_new_time_releases_and_parks()
    {
        var fixture = BookingFixture.Create();
        var bookingId = await ConfirmedBookingAsync(fixture, "IND");
        var booking = fixture.Bookings.Items.Single(b => b.Id == bookingId);
        var canceller = new CancelBookingByAttendeeHandler(
            fixture.Bookings, fixture.Attendees, fixture.Invites, fixture.Events,
            fixture.Locations, fixture.Tokens,
            fixture.UnitOfWork, fixture.Clock, BookingTestZones.Instance,
            fixture.Eligibility, fixture.Settings,
            new InviteIssuer(fixture.Invites, fixture.Settings, fixture.Emails,
                fixture.Audit, fixture.Clock, fixture.Eligibility),
            new BookingCanceller(fixture.Appointments, fixture.Capacities, fixture.Audit));

        var result = await canceller.HandleAsync(
            new CancelBookingByAttendeeCommand(
                fixture.ManageTokenFor(bookingId, booking.ManageTokenVersion), false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("cancelled", result.Value.Outcome);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(10, fixture.RemainingFor("IND"));
        Assert.Equal("NotYetInvited", fixture.Attendees.Items.Single(a => a.Id == booking.AttendeeId).Status.ToString());
    }

    [Fact]
    public async Task Attendee_cancel_with_new_time_supersedes_pending_recovery_and_rebooks()
    {
        var fixture = BookingFixture.Create();
        var bookingId = await ConfirmedBookingAsync(fixture, "IND");
        var booking = fixture.Bookings.Items.Single(b => b.Id == bookingId);
        var attendeeId = booking.AttendeeId;
        var starter = new StartRecoveryShim(fixture);
        var pendingRecoveryId = await starter.StartAsync(attendeeId, bookingId);
        // A second eligible event beyond the cancelled one, so the rebook has somewhere to go.
        fixture.Eligibility.EligibleInOrder = [fixture.EventId, Guid.NewGuid()];
        var canceller = new CancelBookingByAttendeeHandler(
            fixture.Bookings, fixture.Attendees, fixture.Invites, fixture.Events,
            fixture.Locations, fixture.Tokens,
            fixture.UnitOfWork, fixture.Clock, BookingTestZones.Instance,
            fixture.Eligibility, fixture.Settings,
            new InviteIssuer(fixture.Invites, fixture.Settings, fixture.Emails,
                fixture.Audit, fixture.Clock, fixture.Eligibility),
            new BookingCanceller(fixture.Appointments, fixture.Capacities, fixture.Audit));

        var result = await canceller.HandleAsync(
            new CancelBookingByAttendeeCommand(
                fixture.ManageTokenFor(bookingId, booking.ManageTokenVersion), true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("reinvited", result.Value.Outcome);
        Assert.NotEqual(pendingRecoveryId, result.Value.InviteId);
        Assert.Equal(Domain.Invites.InviteStatus.Superseded,
            fixture.Invites.Items.Single(i => i.Id == pendingRecoveryId).Status);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
    }

    [Fact]
    public async Task Attendee_cancel_with_new_time_but_no_options_reports_no_eligible_events()
    {
        var fixture = BookingFixture.Create();
        var bookingId = await ConfirmedBookingAsync(fixture, "IND");
        var booking = fixture.Bookings.Items.Single(b => b.Id == bookingId);
        fixture.Eligibility.EligibleInOrder = [];
        var canceller = new CancelBookingByAttendeeHandler(
            fixture.Bookings, fixture.Attendees, fixture.Invites, fixture.Events,
            fixture.Locations, fixture.Tokens,
            fixture.UnitOfWork, fixture.Clock, BookingTestZones.Instance,
            fixture.Eligibility, fixture.Settings,
            new InviteIssuer(fixture.Invites, fixture.Settings, fixture.Emails,
                fixture.Audit, fixture.Clock, fixture.Eligibility),
            new BookingCanceller(fixture.Appointments, fixture.Capacities, fixture.Audit));

        var result = await canceller.HandleAsync(
            new CancelBookingByAttendeeCommand(
                fixture.ManageTokenFor(bookingId, booking.ManageTokenVersion), true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("noEligibleEvents", result.Value.Outcome);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(10, fixture.RemainingFor("IND"));
        Assert.Equal("NotYetInvited", fixture.Attendees.Items.Single(a => a.Id == booking.AttendeeId).Status.ToString());
    }

    [Fact]
    public async Task Shortfall_with_pending_recovery_rolls_back_and_names_it()
    {
        var fixture = BookingFixture.Create();
        var bookingId = await ConfirmedBookingAsync(fixture, "IND");
        var booking = fixture.Bookings.Items.Single(b => b.Id == bookingId);
        var pendingRecoveryId = await new StartRecoveryShim(fixture).StartAsync(booking.AttendeeId, bookingId);
        fixture.Eligibility.EligibleInOrder = [];
        var canceller = new CancelBookingByAttendeeHandler(
            fixture.Bookings, fixture.Attendees, fixture.Invites, fixture.Events,
            fixture.Locations, fixture.Tokens,
            fixture.UnitOfWork, fixture.Clock, BookingTestZones.Instance,
            fixture.Eligibility, fixture.Settings,
            new InviteIssuer(fixture.Invites, fixture.Settings, fixture.Emails,
                fixture.Audit, fixture.Clock, fixture.Eligibility),
            new BookingCanceller(fixture.Appointments, fixture.Capacities, fixture.Audit));

        var result = await canceller.HandleAsync(
            new CancelBookingByAttendeeCommand(
                fixture.ManageTokenFor(bookingId, booking.ManageTokenVersion), true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("reinvitePending", result.Value.Outcome);
        Assert.Equal(pendingRecoveryId, result.Value.InviteId);
        Assert.Equal(Domain.Invites.InviteStatus.Pending,
            fixture.Invites.Items.Single(i => i.Id == pendingRecoveryId).Status);
    }

    [Fact]
    public async Task Coordinator_cancel_is_two_step_with_active_booking_count()
    {
        var fixture = BookingFixture.Create();
        var bookingId = await ConfirmedBookingAsync(fixture, "IND");
        var booking = fixture.Bookings.Items.Single(b => b.Id == bookingId);
        var handler = new CancelBookingByCoordinatorHandler(
            fixture.Bookings, fixture.Attendees, fixture.Invites, fixture.Events,
            fixture.Locations, fixture.Profiles, fixture.UnitOfWork,
            fixture.Clock, BookingTestZones.Instance,
            new BookingCanceller(fixture.Appointments, fixture.Capacities, fixture.Audit));

        var preview = await handler.HandleAsync(new CancelBookingByCoordinatorCommand(
            fixture.Coordinator, booking.AttendeeId, bookingId, false), CancellationToken.None);

        Assert.True(preview.IsSuccess);
        Assert.True(preview.Value.ConfirmationRequired);
        Assert.Equal(1, preview.Value.ActiveBookingCount);
        Assert.Equal(BookingStatus.Active, booking.Status);

        var confirmed = await handler.HandleAsync(new CancelBookingByCoordinatorCommand(
            fixture.Coordinator, booking.AttendeeId, bookingId, true), CancellationToken.None);

        Assert.True(confirmed.IsSuccess);
        Assert.False(confirmed.Value.ConfirmationRequired);
        Assert.Equal(bookingId, confirmed.Value.CancelledBookingId);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
    }

    // Issues a recovery invite through the Task 14 issuer's recovery operation (exactly the
    // call Task 16's StartRecovery will own).
    private sealed class StartRecoveryShim(BookingFixture fixture)
    {
        public async Task<Guid> StartAsync(Guid attendeeId, Guid bookingId)
        {
            var attendee = fixture.Attendees.Items.Single(a => a.Id == attendeeId);
            var booking = fixture.Bookings.Items.Single(b => b.Id == bookingId);
            var invite = fixture.Invites.Items.Single(i => i.Id == booking.InviteId);
            var issuer = new InviteIssuer(fixture.Invites, fixture.Settings, fixture.Emails,
                fixture.Audit, fixture.Clock, fixture.Eligibility);
            var result = await issuer.IssueRecoveryAsync(attendee, bookingId,
                invite.RequiredAppointmentTypeIds, invite.LocationIds, [fixture.EventId],
                ActorType.Staff, fixture.Coordinator.ToString(), CancellationToken.None);
            Assert.True(result.IsSuccess);
            return result.Value.InviteId;
        }
    }
}
