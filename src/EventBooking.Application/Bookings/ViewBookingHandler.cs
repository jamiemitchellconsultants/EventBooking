using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Bookings;

/// <summary>Defines booking view for the current use case.</summary>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="EndTime">The end time.</param>
/// <param name="Display">The display.</param>
/// <param name="AttendeeName">The attendee name.</param>
public sealed record BookingView(
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Display,
    string AttendeeName);

/// <summary>Defines view booking query for the current use case.</summary>
/// <param name="ManageToken">The manage token.</param>
public sealed record ViewBookingQuery(string? ManageToken);

/// <summary>Defines view booking handler for the current use case.</summary>
/// <param name="bookings">The bookings.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="events">The events.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="locations">The locations.</param>
/// <param name="zones">The zones.</param>
public sealed class ViewBookingHandler(
    IBookingRepository bookings,
    IAttendeeRepository attendees,
    IEventRepository events,
    ITokenService tokens,
    ILocationRepository locations,
    IEventWindowZones zones)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<BookingView>> HandleAsync(
        ViewBookingQuery query,
        CancellationToken cancellationToken)
    {
        if (!tokens.TryRead(query.ManageToken, out var link) || link.Purpose != TokenPurpose.Manage)
        {
            return Result<BookingView>.Failure(Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var booking = await bookings.GetAsync(link.EntityId, cancellationToken);

        if (booking is null
            || booking.ManageTokenVersion != link.Version
            || booking.Status != BookingStatus.Active)
        {
            return Result<BookingView>.Failure(Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var eventItem = await events.GetAsync(booking.EventId, cancellationToken);
        var attendee = await attendees.GetAsync(booking.AttendeeId, cancellationToken);

        if (eventItem is null || attendee is null)
        {
            return Result<BookingView>.Failure(Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var location = await locations.GetAsync(eventItem.LocationId, cancellationToken)
            ?? throw new InvalidOperationException($"Location {eventItem.LocationId} is gone.");

        return Result<BookingView>.Success(new BookingView(
            eventItem.Window.Date,
            eventItem.Window.StartTime,
            eventItem.Window.EndTime,
            WindowText.Format(
                eventItem.Window.Date,
                eventItem.Window.StartTime,
                eventItem.Window.EndTime,
                location.Name,
                zones.AbbreviationOf(
                    eventItem.Window.StartInstant(zones, location.TimeZoneId), location.TimeZoneId)),
            attendee.Name));
    }
}
