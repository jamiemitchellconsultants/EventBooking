using EventBooking.Domain.Common;
using EventBooking.Domain.Time;

namespace EventBooking.Domain.Locations;

/// <summary>
/// An Admin-managed site where events happen, with its own address and IANA time zone. Every
/// window at the site is read in that zone, so the zone cannot move while anything is scheduled.
/// </summary>
public sealed class Location
{
    /// <summary>The longest code a location may have (design 08).</summary>
    public const int MaximumCodeLength = 50;

    /// <summary>The longest name a location may have (design 08).</summary>
    public const int MaximumNameLength = 100;

    /// <summary>The longest address a location may have (design 08).</summary>
    public const int MaximumAddressLength = 500;

    private Location()
    {
        Code = string.Empty;
        Name = string.Empty;
        Address = string.Empty;
        TimeZoneId = string.Empty;
    }

    /// <summary>The stable reference-data identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>The immutable canonical uppercase snake-case code.</summary>
    public string Code { get; private set; }

    /// <summary>The staff-facing display name.</summary>
    public string Name { get; private set; }

    /// <summary>The postal address shown to attendees.</summary>
    public string Address { get; private set; }

    /// <summary>The IANA zone every window at this location is read in.</summary>
    public string TimeZoneId { get; private set; }

    /// <summary>Whether new proposals may name this location.</summary>
    public bool IsActive { get; private set; }

    /// <summary>The optimistic-concurrency token.</summary>
    public long Version { get; private set; }

    /// <summary>Creates an active location.</summary>
    /// <param name="id">The new identifier.</param>
    /// <param name="code">The code, in any case.</param>
    /// <param name="name">The display name.</param>
    /// <param name="address">The postal address.</param>
    /// <param name="timeZoneId">The IANA zone identifier.</param>
    /// <param name="zones">The zone abstraction that says whether the zone exists.</param>
    public static Location Create(
        Guid id, string? code, string? name, string? address, string? timeZoneId, IEventWindowZones zones)
    {
        ArgumentNullException.ThrowIfNull(zones);
        Guard.Against(id == Guid.Empty, "id must not be empty.");

        var canonicalCode = ReferenceDataCode.Parse(code, MaximumCodeLength, "code");
        var displayName = Bounded(name, MaximumNameLength, "name");
        var postalAddress = Bounded(address, MaximumAddressLength, "address");
        var zone = KnownZone(timeZoneId, zones);

        return new Location
        {
            Id = id,
            Code = canonicalCode,
            Name = displayName,
            Address = postalAddress,
            TimeZoneId = zone,
            IsActive = true,
            Version = 1,
        };
    }

    /// <summary>Renames the location. Always allowed.</summary>
    /// <param name="name">The new display name.</param>
    public void Rename(string? name)
    {
        Name = Bounded(name, MaximumNameLength, "name");
        Version++;
    }

    /// <summary>Changes the postal address. Always allowed.</summary>
    /// <param name="address">The new address.</param>
    public void ChangeAddress(string? address)
    {
        Address = Bounded(address, MaximumAddressLength, "address");
        Version++;
    }

    /// <summary>
    /// Moves the location to another zone. Refused while anything is scheduled, because it would
    /// silently move the wall-clock time of every window already agreed (FR-1.2).
    /// </summary>
    /// <param name="timeZoneId">The new IANA zone identifier.</param>
    /// <param name="zones">The zone abstraction that says whether the zone exists.</param>
    /// <param name="usage">What the location currently carries.</param>
    public void ChangeTimeZone(string? timeZoneId, IEventWindowZones zones, LocationUsage usage)
    {
        ArgumentNullException.ThrowIfNull(zones);
        var zone = KnownZone(timeZoneId, zones);

        if (zone == TimeZoneId)
        {
            return;
        }

        if (usage.Any)
        {
            throw new ReferenceDataInUseException(
                "The time zone cannot change while the location has open proposals or future events.",
                usage.AsBlocking());
        }

        TimeZoneId = zone;
        Version++;
    }

    /// <summary>Deactivates the location, refusing while it is in live use (FR-1.6).</summary>
    /// <param name="usage">What the location currently carries.</param>
    public void Deactivate(LocationUsage usage)
    {
        if (usage.Any)
        {
            throw new ReferenceDataInUseException(
                "The location cannot be deactivated while it has open proposals or future events.",
                usage.AsBlocking());
        }

        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        Version++;
    }

    /// <summary>Returns the location to use.</summary>
    public void Reactivate()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        Version++;
    }

    private static string Bounded(string? value, int maximumLength, string field)
    {
        var trimmed = Guard.NotBlank(value, field);
        Guard.Against(trimmed.Length > maximumLength, $"{field} must be at most {maximumLength} characters.");
        return trimmed;
    }

    private static string KnownZone(string? timeZoneId, IEventWindowZones zones)
    {
        var zone = Guard.NotBlank(timeZoneId, "timeZoneId");
        Guard.Against(!zones.IsKnownZone(zone), $"timeZoneId '{zone}' is not a known IANA time zone.");
        return zone;
    }
}
