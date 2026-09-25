// tests/EventBooking.Application.Tests/Bookings/CapacityLockHoldTests.cs (complete)
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Tests.Bookings;
using Xunit;

namespace EventBooking.Application.Tests.Bookings;

public sealed class CapacityLockHoldTests
{
    [Fact]
    public async Task Successful_confirmation_records_one_post_lock_interval()
    {
        var fixture = BookingFixture.Create();
        var (_, inviteId) = fixture.InviteAttendee("IND");
        var invite = fixture.Invites.Items.Single(x => x.Id == inviteId);
        var observer = new RecordingObserver();
        var handler = Handler(fixture, observer);
        var outcome = await handler.HandleAsync(
            new ConfirmBookingCommand(fixture.BookTokenFor(inviteId, invite.TokenVersion), fixture.EventId), default);
        Assert.True(outcome.IsSuccess);
        Assert.Single(observer.Intervals);
        Assert.True(observer.Intervals[0] >= TimeSpan.Zero);
    }

    [Fact]
    public async Task Capacity_refusal_still_records_one_interval()
    {
        var fixture = BookingFixture.Create();
        fixture.Occupy("IND", 10);
        var (_, inviteId) = fixture.InviteAttendee("IND");
        var invite = fixture.Invites.Items.Single(x => x.Id == inviteId);
        var observer = new RecordingObserver();
        var handler = Handler(fixture, observer);
        var outcome = await handler.HandleAsync(
            new ConfirmBookingCommand(fixture.BookTokenFor(inviteId, invite.TokenVersion), fixture.EventId), default);
        Assert.False(outcome.IsSuccess);
        Assert.Single(observer.Intervals);
    }

    [Fact]
    public async Task Invalid_token_never_records_an_interval()
    {
        var fixture = BookingFixture.Create();
        var observer = new RecordingObserver();
        var handler = Handler(fixture, observer);
        var outcome = await handler.HandleAsync(
            new ConfirmBookingCommand("invalid", fixture.EventId), default);
        Assert.False(outcome.IsSuccess);
        Assert.Equal("token-invalid", outcome.Error.Code);
        Assert.Empty(observer.Intervals);
    }

    private sealed class RecordingObserver : ICapacityLockHoldObserver
    {
        public List<TimeSpan> Intervals { get; } = [];
        public void Record(TimeSpan elapsed) => Intervals.Add(elapsed);
    }

    private static ConfirmBookingHandler Handler(BookingFixture fixture, ICapacityLockHoldObserver observer) =>
        new(fixture.Invites, fixture.Attendees, fixture.Events, fixture.Capacities,
            fixture.Bookings, fixture.Appointments, fixture.Locations, fixture.Tokens,
            fixture.Emails, fixture.UnitOfWork, fixture.Audit, fixture.Clock,
            BookingTestZones.Instance, observer);
}
