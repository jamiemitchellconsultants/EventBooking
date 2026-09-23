using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Application.Events;
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Bookings;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Tests.Events;

public sealed class CancelEventHandlerTests
{
    private static CancelEventHandler Handler(BookingFixture f) => new(
        f.Events, f.Capacities, f.Bookings, f.Attendees, f.Invites, f.Locations,
        f.Emails, f.Profiles, f.UnitOfWork, f.Audit, f.Clock,
        BookingTestZones.Instance,
        new InviteIssuer(f.Invites, f.Settings, f.Emails, f.Audit, f.Clock, f.Eligibility),
        f.Eligibility, f.Settings);

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
    public async Task Cancel_event_without_confirm_returns_counts_and_changes_nothing()
    {
        var fixture = BookingFixture.Create();
        await ConfirmedBookingAsync(fixture, "IND");
        await ConfirmedBookingAsync(fixture, "MED");

        var result = await Handler(fixture).HandleAsync(
            new CancelEventCommand(fixture.Coordinator, fixture.EventId, false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.CancelledCount);
        Assert.Equal(Domain.Events.EventStatus.Active, fixture.Events.Items.Single().Status);
        Assert.Equal(2, fixture.Bookings.Items.Count(b => b.Status == BookingStatus.Active));
    }

    [Fact]
    public async Task Cancel_event_with_confirm_cancels_in_attendee_order_with_replacements()
    {
        var fixture = BookingFixture.Create();
        var first = await ConfirmedBookingAsync(fixture, "IND");
        var second = await ConfirmedBookingAsync(fixture, "MED");
        // Replacement options beyond the cancelled event, so every attendee is rebooked.
        fixture.Eligibility.EligibleInOrder = [fixture.EventId, Guid.NewGuid(), Guid.NewGuid()];

        var result = await Handler(fixture).HandleAsync(
            new CancelEventCommand(fixture.Coordinator, fixture.EventId, true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.CancelledCount);
        Assert.Equal(2, result.Value.ReinvitedCount);
        Assert.Equal(0, result.Value.AwaitingAvailabilityCount);
        Assert.Equal(Domain.Events.EventStatus.Cancelled, fixture.Events.Items.Single().Status);
        var orderedCancels = fixture.Audit.Entries
            .Where(e => e.Action == AuditAction.BookingCancelled)
            .Select(e => e.EntityId).ToList();
        var firstAttendee = fixture.Bookings.Items.Single(b => b.Id == first).AttendeeId;
        var secondAttendee = fixture.Bookings.Items.Single(b => b.Id == second).AttendeeId;
        var expectedFirst = firstAttendee.CompareTo(secondAttendee) < 0 ? first : second;
        Assert.Equal(expectedFirst, orderedCancels[0]);
        Assert.All(fixture.Audit.Entries.Where(e => e.Action == AuditAction.BookingCancelled),
            e => Assert.Contains("replacement created", e.Details));
        Assert.Equal(2, fixture.Emails.Items.Count(e =>
            e.TemplateName == EmailTemplate.EventCancelledRebookingNeeded));
    }

    [Fact]
    public async Task Manager_scoped_to_unlisted_type_cannot_cancel_event()
    {
        var fixture = BookingFixture.Create();
        var esc = Domain.AppointmentTypes.AppointmentType.Create(Guid.NewGuid(), "ESC", "Escalation");
        fixture.Types.Items.Add(esc);
        var escManager = Guid.NewGuid();
        fixture.Profiles.Add(Domain.Access.StaffAccessProfile.Create(
            escManager, Domain.Access.Role.Manager, esc.Id));

        var result = await Handler(fixture).HandleAsync(
            new CancelEventCommand(escManager, fixture.EventId, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(Domain.Events.EventStatus.Active, fixture.Events.Items.Single().Status);
    }
}
