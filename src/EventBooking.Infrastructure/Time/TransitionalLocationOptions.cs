namespace EventBooking.Infrastructure.Time;

/// <summary>Single site, single zone. An identifier the host operating system recognises.</summary>
public sealed record TransitionalLocationOptions(string TimeZoneId);
