using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Bookings;

/// <summary>Defines booking view for the current use case.</summary>
/// <param name="LocationName">The location display name.</param>
/// <param name="Address">The location address.</param>
/// <param name="TimeZoneId">The location's time zone.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="DurationMinutes">The window length.</param>
/// <param name="AppointmentTypeNames">The booked appointment type names.</param>
/// <param name="AttendeeName">The attendee name.</param>
/// <param name="CanCancel">Whether the window has not started and cancellation is still possible.</param>
public sealed record BookingView(
    string LocationName,
    string Address,
    string TimeZoneId,
    DateOnly Date,
    TimeOnly StartTime,
    int DurationMinutes,
    IReadOnlyList<string> AppointmentTypeNames,
    string AttendeeName,
    bool CanCancel);

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
/// <param name="clock">The clock the cancellation window is read against.</param>
/// <param name="invites">The invites the booking's type names are read from.</param>
/// <param name="types">The appointment types names resolve against.</param>
public sealed class ViewBookingHandler(
    IBookingRepository bookings,
    IAttendeeRepository attendees,
    IEventRepository events,
    ITokenService tokens,
    ILocationRepository locations,
    IEventWindowZones zones,
    IClock clock,
    IInviteRepository invites,
    IAppointmentTypeRepository types)
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
            return Result<BookingView>.Failure(Error.TokenInvalid(ViewInviteHandler.InvalidLinkMessage));
        }

        var booking = await bookings.GetAsync(link.EntityId, cancellationToken);

        if (booking is null
            || booking.ManageTokenVersion != link.Version
            || booking.Status != BookingStatus.Active)
        {
            return Result<BookingView>.Failure(Error.TokenInvalid(ViewInviteHandler.InvalidLinkMessage));
        }

        var eventItem = await events.GetAsync(booking.EventId, cancellationToken);
        var attendee = await attendees.GetAsync(booking.AttendeeId, cancellationToken);

        if (eventItem is null || attendee is null)
        {
            return Result<BookingView>.Failure(Error.TokenInvalid(ViewInviteHandler.InvalidLinkMessage));
        }

        // Design 06: nothing invalidates a manage token in the first release, so there is no
        // lapsed state to disclose and every failure is one answer — including a location the
        // database no longer holds, which the holder experiences as a link that shows nothing.
        var location = await locations.GetAsync(eventItem.LocationId, cancellationToken);
        if (location is null)
        {
            return Result<BookingView>.Failure(Error.TokenInvalid(ViewInviteHandler.InvalidLinkMessage));
        }

        // Advisory only: the cancellation call judges, including any active recovery's
        // window, which this read does not lock. A true flag with a started recovery window
        // still comes back window-started.
        var canCancel = !eventItem.Window.HasStarted(zones, location.TimeZoneId, clock.UtcNow);
        var invite = await invites.GetAsync(booking.InviteId, cancellationToken);
        if (invite is null)
        {
            return Result<BookingView>.Failure(Error.TokenInvalid(ViewInviteHandler.InvalidLinkMessage));
        }

        var names = (await types.ListAsync(cancellationToken)).ToDictionary(t => t.Id, t => t.Name);
        return Result<BookingView>.Success(new BookingView(
            location.Name,
            location.Address,
            location.TimeZoneId,
            eventItem.Window.Date,
            eventItem.Window.StartTime,
            eventItem.Window.DurationMinutes,
            invite.RequiredAppointmentTypeIds
                .Select(id => names.GetValueOrDefault(id, id.ToString()))
                .ToList(),
            attendee.Name,
            canCancel));
    }
}
