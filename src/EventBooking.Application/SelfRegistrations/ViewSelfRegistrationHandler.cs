using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.SelfRegistrations;

namespace EventBooking.Application.SelfRegistrations;

/// <summary>Views one pending request through its token.</summary>
/// <param name="Token">The signed confirmation token.</param>
public sealed record ViewSelfRegistrationQuery(string? Token);

/// <summary>The pending request's public summary.</summary>
/// <param name="EventGroupId">The owning event group identifier.</param>
/// <param name="EventId">The requested event identifier.</param>
/// <param name="EventGroupTitle">The owning event group title.</param>
/// <param name="LocationName">The event location name.</param>
/// <param name="Date">The event local date.</param>
/// <param name="StartTime">The event local start time.</param>
/// <param name="AttendeeGroupName">The requested attendee group name.</param>
public sealed record SelfRegistrationSummary(
    Guid EventGroupId, Guid EventId, string EventGroupTitle, string LocationName,
    DateOnly Date, TimeOnly StartTime, string AttendeeGroupName);

/// <summary>Reads one request's public summary without changing anything.</summary>
/// <param name="groups">The event groups.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="attendeeGroups">The attendee groups.</param>
/// <param name="events">The events.</param>
/// <param name="locations">The locations.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="clock">The clock.</param>
public sealed class ViewSelfRegistrationHandler(
    IEventGroupRepository groups,
    IAttendeeRepository attendees,
    IBookingRepository bookings,
    IAttendeeGroupRepository attendeeGroups,
    IEventRepository events,
    ILocationRepository locations,
    ITokenService tokens,
    IClock clock)
{
    private const string InvalidLinkMessage = "This confirmation link is invalid.";
    private const string ExpiredLinkMessage = "This confirmation link has expired.";

    /// <summary>Handles the query.</summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<SelfRegistrationSummary>> HandleAsync(
        ViewSelfRegistrationQuery query, CancellationToken ct)
    {
        if (!SelfRegistrationToken.TryRead(
                tokens, query.Token, out var requestId, out var tokenVersion))
            return Result<SelfRegistrationSummary>.Failure(
                Error.TokenInvalid(InvalidLinkMessage));

        var registration = await groups.GetRegistrationAsync(requestId, ct);
        if (registration is null)
            return Result<SelfRegistrationSummary>.Failure(
                Error.TokenInvalid(InvalidLinkMessage));

        if (registration.Status == SelfRegistrationStatus.Confirmed)
        {
            var attendee = await attendees.GetByEmailAsync(registration.Email, ct);
            var booking = attendee is null
                ? null
                : await bookings.GetActiveForAttendeeAsync(attendee.Id, ct);
            if (booking is not null && booking.EventId == registration.EventId)
                return Result<SelfRegistrationSummary>.Failure(Error.AlreadyConfirmed(
                    $"This request already confirmed booking {booking.Id}.", booking.Id));
            return Result<SelfRegistrationSummary>.Failure(
                Error.TokenInvalid(InvalidLinkMessage));
        }

        if (registration.Status == SelfRegistrationStatus.Expired
            || clock.UtcNow >= registration.ExpiresAt)
            return Result<SelfRegistrationSummary>.Failure(
                Error.TokenExpired(ExpiredLinkMessage));

        if (tokenVersion != registration.TokenVersion)
            return Result<SelfRegistrationSummary>.Failure(
                Error.TokenInvalid(InvalidLinkMessage));

        var group = await groups.GetAsync(registration.EventGroupId, ct);
        if (group is null)
            return Result<SelfRegistrationSummary>.Failure(
                Error.TokenInvalid(InvalidLinkMessage));

        var eventItem = await events.GetAsync(registration.EventId, ct);
        if (eventItem is null)
            return Result<SelfRegistrationSummary>.Failure(
                Error.NotFound("No such event."));

        var location = await locations.GetAsync(eventItem.LocationId, ct);
        if (location is null)
            return Result<SelfRegistrationSummary>.Failure(
                Error.NotFound("No such location."));

        var attendeeGroup = await attendeeGroups.GetAsync(registration.AttendeeGroupId, ct);
        if (attendeeGroup is null || !attendeeGroup.IsActive)
            return Result<SelfRegistrationSummary>.Failure(
                SelfRegistrationErrors.GroupNotSelectable());

        return Result<SelfRegistrationSummary>.Success(new SelfRegistrationSummary(
            registration.EventGroupId, registration.EventId, group.Title, location.Name,
            eventItem.Window.Date, eventItem.Window.StartTime, attendeeGroup.Name));
    }
}
