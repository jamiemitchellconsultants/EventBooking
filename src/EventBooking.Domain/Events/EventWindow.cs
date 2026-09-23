using EventBooking.Domain.Common;
using EventBooking.Domain.Time;

namespace EventBooking.Domain.Events;

/// <summary>
/// The attendee-facing window: a local date, a local start time and an explicit duration, all read
/// in the <c>Location</c>'s zone. The end time is always derived, never stored.
/// </summary>
public sealed record EventWindow : IComparable<EventWindow>
{
    /// <summary>The smallest step a duration may take, and the granularity of every duration.</summary>
    public const int DurationStepMinutes = 15;

    /// <summary>The longest window the design allows (design 08 — boundary values).</summary>
    public const int MaximumDurationMinutes = 720;

    /// <summary>Creates a window, refusing any duration the design does not allow.</summary>
    /// <param name="date">The local date the window starts and ends on.</param>
    /// <param name="startTime">The local start time.</param>
    /// <param name="durationMinutes">The length in minutes: a multiple of 15, from 15 to 720.</param>
    public EventWindow(DateOnly date, TimeOnly startTime, int durationMinutes)
    {
        Guard.Against(
            durationMinutes < DurationStepMinutes || durationMinutes > MaximumDurationMinutes,
            $"durationMinutes must be between {DurationStepMinutes} and {MaximumDurationMinutes}.");
        Guard.Against(
            durationMinutes % DurationStepMinutes != 0,
            $"durationMinutes must be a multiple of {DurationStepMinutes}.");
        Guard.Against(
            startTime.ToTimeSpan() + TimeSpan.FromMinutes(durationMinutes) >= TimeSpan.FromHours(24),
            "The window must end on the local date it starts.");

        Date = date;
        StartTime = startTime;
        DurationMinutes = durationMinutes;
    }

    /// <summary>The local date the window starts and ends on.</summary>
    public DateOnly Date { get; }

    /// <summary>The local start time.</summary>
    public TimeOnly StartTime { get; }

    /// <summary>The length of the window in minutes.</summary>
    public int DurationMinutes { get; }

    /// <summary>The derived local end time, always on the same date.</summary>
    public TimeOnly EndTime => StartTime.Add(TimeSpan.FromMinutes(DurationMinutes));

    /// <summary>Whether the window's date falls after the given local date.</summary>
    /// <param name="today">The local date to compare against.</param>
    public bool StartsAfter(DateOnly today) => Date > today;

    /// <summary>The instant the window starts, in the given zone.</summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    public DateTimeOffset StartInstant(IEventWindowZones zones, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(zones);
        return zones.InstantOf(Date, StartTime, timeZoneId);
    }

    /// <summary>The instant the window ends, in the given zone.</summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    public DateTimeOffset EndInstant(IEventWindowZones zones, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(zones);
        return zones.InstantOf(Date, EndTime, timeZoneId);
    }

    /// <summary>Whether the window has started at the given instant.</summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="now">The current instant.</param>
    public bool HasStarted(IEventWindowZones zones, string timeZoneId, DateTimeOffset now) =>
        now >= StartInstant(zones, timeZoneId);

    /// <summary>Whether the window has ended at the given instant.</summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="now">The current instant.</param>
    public bool HasEnded(IEventWindowZones zones, string timeZoneId, DateTimeOffset now) =>
        now >= EndInstant(zones, timeZoneId);

    /// <summary>Whether the given instant falls on the window's own local date.</summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="now">The current instant.</param>
    public bool IsOnEventDate(IEventWindowZones zones, string timeZoneId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(zones);
        return zones.LocalDateOf(now, timeZoneId) == Date;
    }

    /// <summary>
    /// Why this window cannot be interpreted in the zone, or None. A window whose start or end
    /// falls in a daylight-saving gap or overlap does not identify a unique instant, so it is
    /// refused when a proposal is made rather than resolved arbitrarily.
    /// </summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    public EventWindowZoneProblem ProblemIn(IEventWindowZones zones, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(zones);

        if (!zones.IsKnownZone(timeZoneId))
        {
            return EventWindowZoneProblem.UnknownZone;
        }

        if (zones.ValidityOf(Date, StartTime, timeZoneId) != LocalTimeValidity.Unique)
        {
            return EventWindowZoneProblem.StartHasNoUniqueInstant;
        }

        return zones.ValidityOf(Date, EndTime, timeZoneId) != LocalTimeValidity.Unique
            ? EventWindowZoneProblem.EndHasNoUniqueInstant
            : EventWindowZoneProblem.None;
    }

    /// <summary>Orders by local date, then local start time.</summary>
    /// <param name="other">The window to compare against.</param>
    public int CompareTo(EventWindow? other)
    {
        if (other is null)
        {
            return 1;
        }

        var byDate = Date.CompareTo(other.Date);
        return byDate != 0 ? byDate : StartTime.CompareTo(other.StartTime);
    }

    /// <summary>Renders the local window for logs and diagnostics.</summary>
    public override string ToString() =>
        $"{Date:yyyy-MM-dd} {StartTime:HH\\:mm}-{EndTime:HH\\:mm}";
}
