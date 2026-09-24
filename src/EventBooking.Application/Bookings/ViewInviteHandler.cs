using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Bookings;

/// <summary>Defines invite option view for the current use case.</summary>
/// <param name="EventId">The event id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="EndTime">The end time.</param>
/// <param name="Display">The display.</param>
public sealed record InviteOptionView(
    Guid EventId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Display);

/// <summary>Defines invite view for the current use case.</summary>
/// <param name="InviteId">The invite id.</param>
/// <param name="AttendeeName">The attendee name.</param>
/// <param name="AppointmentTypeNames">The appointment type names.</param>
/// <param name="Options">The options.</param>
/// <param name="IsRecovery">The is recovery.</param>
public sealed record InviteView(
    Guid InviteId,
    string AttendeeName,
    IReadOnlyList<string> AppointmentTypeNames,
    IReadOnlyList<InviteOptionView> Options,
    bool IsRecovery);

/// <summary>Defines view invite query for the current use case.</summary>
/// <param name="Token">The token.</param>
public sealed record ViewInviteQuery(string? Token);

/// <summary>Defines view invite handler for the current use case.</summary>
/// <param name="invites">The invites.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="events">The events.</param>
/// <param name="eventFinder">The event finder.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="clock">The clock.</param>
/// <param name="locations">The locations.</param>
/// <param name="zones">The zones.</param>
/// <param name="types">Resolves requirement names for the invite view.</param>
public sealed class ViewInviteHandler(
    IInviteRepository invites,
    IAttendeeRepository attendees,
    IEventRepository events,
    EligibleEventFinder eventFinder,
    IAuditLogger audit,
    IUnitOfWork unitOfWork,
    ITokenService tokens,
    IClock clock,
    ILocationRepository locations,
    IEventWindowZones zones,
    IAppointmentTypeRepository types)
{
    /// <summary>
    /// One message for every failure except a lapsed expiry. A caller must not be able to tell
    /// a forged token from a used, superseded or cancelled one.
    /// </summary>
    public const string InvalidLinkMessage = "This booking link is no longer valid.";

    /// <summary>The one disclosure: a real link that has lapsed, so the page can say who to ask.</summary>
    public const string ExpiredLinkMessage = "This booking link has expired.";

    /// <summary>
    /// Projects the usable future appointment options for the supplied attendee invite token.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<InviteView>> HandleAsync(
        ViewInviteQuery query,
        CancellationToken cancellationToken)
    {
        if (!tokens.TryRead(query.Token, out var link) || link.Purpose != TokenPurpose.Book)
        {
            return Result<InviteView>.Failure(Error.TokenInvalid(InvalidLinkMessage));
        }

        var invite = await invites.GetAsync(link.EntityId, cancellationToken);
        if (invite is null || invite.TokenVersion != link.Version)
        {
            return Result<InviteView>.Failure(Error.TokenInvalid(InvalidLinkMessage));
        }

        // The signature verified, the row resolved and the stored version matched, so the holder
        // demonstrably received this link. Telling them it has lapsed discloses nothing they did not
        // already know and is what design 06's expired page needs. A Used, Superseded or Cancelled
        // invite stays indistinguishable from a forgery, because those states are not the holder's.
        if (!invite.IsUsableAt(clock.UtcNow))
        {
            return Result<InviteView>.Failure(
                invite.Status == InviteStatus.Pending
                    ? Error.TokenExpired(ExpiredLinkMessage)
                    : Error.TokenInvalid(InvalidLinkMessage));
        }

        var attendee = await attendees.GetAsync(invite.AttendeeId, cancellationToken);
        if (attendee is null)
        {
            return Result<InviteView>.Failure(Error.TokenInvalid(InvalidLinkMessage));
        }

        var required = invite.RequiredAppointmentTypeIds;
        var today = clock.TodayAtTransitionalLocation;

        var options = new List<Event>();
        var deadEventIds = new List<Guid>();
        foreach (var eventId in invite.OfferedEventIds)
        {
            var eventItem = await events.GetAsync(eventId, cancellationToken);
            if (eventItem is not null
                && eventItem.Status == EventStatus.Active
                && eventItem.Window.StartsAfter(today)
                && HasSpareFor(eventItem, required))
            {
                options.Add(eventItem);
            }
            else
            {
                deadEventIds.Add(eventId);
            }
        }

        var mutated = false;
        if (deadEventIds.Count > 0)
        {
            foreach (var deadEventId in deadEventIds)
            {
                invite.RemoveOption(deadEventId);
            }

            var replacements = await eventFinder.FindAsync(
                required,
                deadEventIds.Count,
                invite.OfferedEventIds.Concat(deadEventIds).ToList(),
                cancellationToken);

            foreach (var replacement in replacements)
            {
                if (replacement is null)
                {
                    continue;
                }

                invite.AddOption(replacement.Id);
                options.Add(replacement);
                mutated = true;

                audit.Record(
                    AuditEntityTypes.Invite,
                    invite.Id,
                    AuditAction.InviteOptionReplaced,
                    ActorType.AttendeeToken,
                    invite.Id.ToString(),
                    $"{deadEventIds.Count} lost option(s) replaced by {replacement.Id}");
            }

            mutated = true;
        }

        if (options.Count < invite.InviteOptionCount
            && attendee.Status == AttendeeStatus.Invited)
        {
            attendee.MarkNoResponse(clock.UtcNow);
            mutated = true;

            audit.Record(
                AuditEntityTypes.Invite,
                invite.Id,
                AuditAction.InviteOptionReplaced,
                ActorType.AttendeeToken,
                invite.Id.ToString(),
                $"only {options.Count} live option(s) remain, attendee flagged for follow-up");
        }

        if (mutated)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var optionViews = new List<InviteOptionView>();
        foreach (var option in options.OrderBy(s => s.Window))
        {
            var location = await locations.GetAsync(option.LocationId, cancellationToken);
            if (location is null)
            {
                return Result<InviteView>.Failure(Error.TokenInvalid(InvalidLinkMessage));
            }
            optionViews.Add(new InviteOptionView(
                option.Id,
                option.Window.Date,
                option.Window.StartTime,
                option.Window.EndTime,
                WindowText.Format(
                    option.Window.Date,
                    option.Window.StartTime,
                    option.Window.EndTime,
                    location.Name,
                    zones.AbbreviationOf(
                        option.Window.StartInstant(zones, location.TimeZoneId),
                        location.TimeZoneId))));
        }

        // Resolved from the stored rows, not the canonical constants: invites snapshot
        // whatever types exist when they are issued, including Admin-created ones.
        var names = (await types.ListAsync(cancellationToken)).ToDictionary(t => t.Id, t => t.Name);
        var view = new InviteView(
            invite.Id,
            attendee.Name,
            invite.RequiredAppointmentTypeIds
                .Select(id => names.GetValueOrDefault(id, id.ToString()))
                .ToList(),
            optionViews,
            invite.RecoveryOfBookingId is not null);

        return Result<InviteView>.Success(view);
    }

    /// <summary>
    /// Determines whether the event holds spare capacity for every snapshotted requirement.
    /// A missing capacity row is treated as no spare capacity rather than throwing.
    /// </summary>
    private static bool HasSpareFor(Event eventItem, IReadOnlyList<Guid> required)
    {
        try
        {
            return eventItem.HasSpareCapacityForAll(required);
        }
        catch (DomainException)
        {
            return false;
        }
    }
}
