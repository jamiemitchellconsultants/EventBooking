using EventBooking.Domain.Time;
using NodaTime;

namespace EventBooking.Infrastructure.Time;

/// <summary>
/// Answers the domain's time-zone questions from the IANA database. The rules are versioned data,
/// not arithmetic: a gap or an overlap is a property of the zone at that instant, so the domain
/// asks rather than computes.
/// </summary>
public sealed class NodaTimeEventWindowZones : IEventWindowZones
{
    private readonly IDateTimeZoneProvider _provider = DateTimeZoneProviders.Tzdb;

    /// <inheritdoc/>
    public bool IsKnownZone(string timeZoneId) =>
        !string.IsNullOrWhiteSpace(timeZoneId) && _provider.GetZoneOrNull(timeZoneId) is not null;

    /// <inheritdoc/>
    public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId)
    {
        var mapping = Zone(timeZoneId).MapLocal(Local(date, time));
        return mapping.Count switch
        {
            0 => LocalTimeValidity.Gap,
            1 => LocalTimeValidity.Unique,
            _ => LocalTimeValidity.Ambiguous,
        };
    }

    /// <inheritdoc/>
    public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId)
    {
        // Lenient resolution would silently move a gap time forward and pick the earlier of an
        // ambiguous pair. The domain refuses such windows at proposal time, so reaching here with
        // one is a defect worth surfacing rather than smoothing over.
        var mapping = Zone(timeZoneId).MapLocal(Local(date, time));
        if (mapping.Count != 1)
        {
            throw new InvalidOperationException(
                $"{date:yyyy-MM-dd} {time:HH}:{time:mm} does not name exactly one instant in {timeZoneId}.");
        }

        return mapping.Single().ToDateTimeOffset();
    }

    /// <inheritdoc/>
    public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId)
    {
        var local = Instant.FromDateTimeOffset(instant).InZone(Zone(timeZoneId)).LocalDateTime;
        return new DateOnly(local.Year, local.Month, local.Day);
    }

    /// <inheritdoc/>
    public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) =>
        Zone(timeZoneId).GetZoneInterval(Instant.FromDateTimeOffset(instant)).Name;

    private static LocalDateTime Local(DateOnly date, TimeOnly time) =>
        new(date.Year, date.Month, date.Day, time.Hour, time.Minute);

    private DateTimeZone Zone(string timeZoneId) =>
        _provider.GetZoneOrNull(timeZoneId)
        ?? throw new InvalidOperationException($"Unknown time zone '{timeZoneId}'.");
}
