namespace EventBooking.Domain.Locations;

/// <summary>
/// The well-known location the initial migration seeds and the fixtures re-seed after every
/// reset. Negotiation handlers no longer default to it — the command carries the location —
/// but the seeded row keeps this identifier so existing databases and tests agree.
/// </summary>
public static class TransitionalLocation
{
    /// <summary>The identifier of the migration-seeded transitional location.</summary>
    public static Guid Id { get; } = Guid.Parse("10000000-0000-0000-0000-000000000001");

    /// <summary>The zone that location is read in, matching the transitional clock.</summary>
    public const string TimeZoneId = "Europe/London";
}
