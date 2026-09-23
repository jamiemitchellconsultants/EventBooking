namespace EventBooking.Application.Events;

/// <summary>
/// Proposals name the location that would host them from Task 6, but nothing carries a location
/// into the handlers until Task 13 puts it on the command, the API and the MCP tool. Until then
/// every proposal is made at this one well-known location, exactly as the predecessor's single
/// site behaved. Delete this class in Task 13.
/// </summary>
public static class TransitionalLocation
{
    /// <summary>The single location every proposal is made at until Task 13.</summary>
    public static Guid Id { get; } = Guid.Parse("10000000-0000-0000-0000-000000000001");

    /// <summary>The zone that location is read in, matching the transitional clock.</summary>
    public const string TimeZoneId = "Europe/London";
}
